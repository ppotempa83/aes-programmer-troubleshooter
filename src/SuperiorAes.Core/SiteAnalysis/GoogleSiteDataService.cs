using System.Globalization;
using System.Text.Json;
using SuperiorAes.Core.Models;

namespace SuperiorAes.Core.SiteAnalysis;

public sealed class GoogleSiteDataService
{
    private readonly HttpClient _httpClient;

    public GoogleSiteDataService(HttpClient? httpClient = null)
    {
        _httpClient = httpClient ?? new HttpClient { Timeout = TimeSpan.FromSeconds(20) };
    }

    public async Task<BuildingSiteData> AnalyzeAsync(
        string address,
        string apiKey,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(address);
        ArgumentException.ThrowIfNullOrWhiteSpace(apiKey);

        var geocodeUrl =
            $"https://maps.googleapis.com/maps/api/geocode/json?address={Uri.EscapeDataString(address)}&key={Uri.EscapeDataString(apiKey)}";
        using var geocode = await GetDocumentAsync(geocodeUrl, cancellationToken);
        EnsureGoogleStatus(geocode.RootElement, "geocoding");
        var result = geocode.RootElement.GetProperty("results")[0];
        var location = result.GetProperty("geometry").GetProperty("location");
        var latitude = location.GetProperty("lat").GetDouble();
        var longitude = location.GetProperty("lng").GetDouble();
        var formattedAddress = result.GetProperty("formatted_address").GetString() ?? address;

        double? groundElevation = null;
        double? roofElevation = null;
        double? roofArea = null;
        double? roofPitch = null;
        double? roofAzimuth = null;
        var imageryQuality = "Not available";
        var notes = new List<string>();

        try
        {
            var elevationUrl = string.Create(
                CultureInfo.InvariantCulture,
                $"https://maps.googleapis.com/maps/api/elevation/json?locations={latitude},{longitude}&key={Uri.EscapeDataString(apiKey)}");
            using var elevation = await GetDocumentAsync(elevationUrl, cancellationToken);
            EnsureGoogleStatus(elevation.RootElement, "elevation");
            groundElevation = elevation.RootElement.GetProperty("results")[0].GetProperty("elevation").GetDouble();
        }
        catch (Exception exception) when (exception is HttpRequestException or InvalidOperationException)
        {
            notes.Add($"Elevation unavailable: {exception.Message}");
        }

        try
        {
            var solarUrl = string.Create(
                CultureInfo.InvariantCulture,
                $"https://solar.googleapis.com/v1/buildingInsights:findClosest?location.latitude={latitude}&location.longitude={longitude}&requiredQuality=BASE&key={Uri.EscapeDataString(apiKey)}");
            using var solar = await GetDocumentAsync(solarUrl, cancellationToken);
            var root = solar.RootElement;
            imageryQuality = root.TryGetProperty("imageryQuality", out var quality)
                ? quality.GetString() ?? "Unknown"
                : "Unknown";

            if (root.TryGetProperty("solarPotential", out var potential))
            {
                roofArea = TryGetDouble(potential, "wholeRoofStats", "areaMeters2");
                if (potential.TryGetProperty("roofSegmentStats", out var segments))
                {
                    var segmentValues = segments.EnumerateArray().ToArray();
                    roofElevation = segmentValues
                        .Select(segment => TryGetDouble(segment, "planeHeightAtCenterMeters"))
                        .Where(value => value.HasValue)
                        .Max();
                    var largest = segmentValues
                        .OrderByDescending(segment => TryGetDouble(segment, "stats", "areaMeters2") ?? 0)
                        .FirstOrDefault();
                    if (largest.ValueKind != JsonValueKind.Undefined)
                    {
                        roofPitch = TryGetDouble(largest, "pitchDegrees");
                        roofAzimuth = TryGetDouble(largest, "azimuthDegrees");
                    }
                }
            }
        }
        catch (Exception exception) when (exception is HttpRequestException or InvalidOperationException)
        {
            notes.Add($"Building imagery insight unavailable: {exception.Message}");
        }

        double? height = roofElevation.HasValue && groundElevation.HasValue
            ? Math.Max(0, roofElevation.Value - groundElevation.Value)
            : null;
        notes.Add("Google Solar Building Insights is imagery-derived planning data, not a structural survey.");

        return new BuildingSiteData(
            formattedAddress,
            latitude,
            longitude,
            groundElevation,
            roofElevation,
            height,
            roofArea,
            roofPitch,
            roofAzimuth,
            imageryQuality,
            string.Join(" ", notes));
    }

    private async Task<JsonDocument> GetDocumentAsync(string url, CancellationToken cancellationToken)
    {
        using var response = await _httpClient.GetAsync(url, cancellationToken);
        var payload = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var message = TryReadError(payload) ?? $"{(int)response.StatusCode} {response.ReasonPhrase}";
            throw new HttpRequestException(message);
        }

        return JsonDocument.Parse(payload);
    }

    private static void EnsureGoogleStatus(JsonElement root, string operation)
    {
        var status = root.TryGetProperty("status", out var statusElement)
            ? statusElement.GetString()
            : "OK";
        if (string.Equals(status, "OK", StringComparison.Ordinal))
        {
            return;
        }

        var message = root.TryGetProperty("error_message", out var error)
            ? error.GetString()
            : null;
        throw new InvalidOperationException(
            $"Google {operation} returned {status}{(string.IsNullOrWhiteSpace(message) ? "." : $": {message}")}");
    }

    private static double? TryGetDouble(JsonElement root, params string[] path)
    {
        var current = root;
        foreach (var name in path)
        {
            if (!current.TryGetProperty(name, out current))
            {
                return null;
            }
        }

        return current.ValueKind == JsonValueKind.Number ? current.GetDouble() : null;
    }

    private static string? TryReadError(string payload)
    {
        try
        {
            using var document = JsonDocument.Parse(payload);
            var root = document.RootElement;
            if (root.TryGetProperty("error", out var error) &&
                error.TryGetProperty("message", out var message))
            {
                return message.GetString();
            }

            return root.TryGetProperty("error_message", out var legacy)
                ? legacy.GetString()
                : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
