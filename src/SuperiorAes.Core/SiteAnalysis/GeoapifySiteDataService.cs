using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json;
using SuperiorAes.Core.Models;

namespace SuperiorAes.Core.SiteAnalysis;

public sealed class GeoapifySiteDataService
{
    private readonly HttpClient _httpClient;

    public GeoapifySiteDataService(HttpClient? httpClient = null)
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
            $"https://api.geoapify.com/v1/geocode/search?text={Uri.EscapeDataString(address)}&format=json&limit=1&apiKey={Uri.EscapeDataString(apiKey)}";
        using var geocode = await GetDocumentAsync(geocodeUrl, cancellationToken);
        if (!geocode.RootElement.TryGetProperty("results", out var results) ||
            results.GetArrayLength() == 0)
        {
            throw new InvalidOperationException("Geoapify could not find that address.");
        }

        var result = results[0];
        var latitude = result.GetProperty("lat").GetDouble();
        var longitude = result.GetProperty("lon").GetDouble();
        var formattedAddress = result.TryGetProperty("formatted", out var formatted)
            ? formatted.GetString() ?? address
            : address;

        double? groundElevation = null;
        var notes = new List<string>();
        try
        {
            var elevationUrl =
                $"https://api.geoapify.com/v1/geodata/elevation?apiKey={Uri.EscapeDataString(apiKey)}";
            using var request = new HttpRequestMessage(HttpMethod.Post, elevationUrl)
            {
                Content = JsonContent.Create(new
                {
                    format = "json",
                    units = "metric",
                    locations = new[] { new { lat = latitude, lon = longitude } }
                })
            };
            using var response = await _httpClient.SendAsync(request, cancellationToken);
            var payload = await response.Content.ReadAsStringAsync(cancellationToken);
            EnsureSuccess(response, payload);
            using var elevation = JsonDocument.Parse(payload);
            if (elevation.RootElement.TryGetProperty("results", out var elevationResults) &&
                elevationResults.GetArrayLength() > 0 &&
                elevationResults[0].TryGetProperty("elevation", out var elevationValue))
            {
                groundElevation = elevationValue.GetDouble();
            }
        }
        catch (Exception exception) when (exception is HttpRequestException or InvalidOperationException)
        {
            notes.Add($"Terrain elevation unavailable: {exception.Message}");
        }

        notes.Add("Geoapify provides address and terrain elevation data. Roof height, area, pitch, and structural details must be verified on site.");
        return new BuildingSiteData(
            formattedAddress,
            latitude,
            longitude,
            groundElevation,
            null,
            null,
            null,
            null,
            null,
            "Geoapify address and terrain data",
            string.Join(" ", notes));
    }

    public async Task<byte[]> GetStaticMapAsync(
        double latitude,
        double longitude,
        string apiKey,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(apiKey);
        var center = string.Create(CultureInfo.InvariantCulture, $"{longitude},{latitude}");
        var url =
            $"https://maps.geoapify.com/v1/staticmap?style=osm-bright&width=900&height=360&center=lonlat:{center}&zoom=16&marker=lonlat:{center};color:%23d52736;size:large&apiKey={Uri.EscapeDataString(apiKey)}";
        using var response = await _httpClient.GetAsync(url, cancellationToken);
        var payload = await response.Content.ReadAsByteArrayAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException($"Geoapify static map returned {(int)response.StatusCode} {response.ReasonPhrase}.");
        }

        return payload;
    }

    private async Task<JsonDocument> GetDocumentAsync(string url, CancellationToken cancellationToken)
    {
        using var response = await _httpClient.GetAsync(url, cancellationToken);
        var payload = await response.Content.ReadAsStringAsync(cancellationToken);
        EnsureSuccess(response, payload);
        return JsonDocument.Parse(payload);
    }

    private static void EnsureSuccess(HttpResponseMessage response, string payload)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        var message = TryReadError(payload) ?? $"{(int)response.StatusCode} {response.ReasonPhrase}";
        throw new HttpRequestException($"Geoapify request failed: {message}");
    }

    private static string? TryReadError(string payload)
    {
        try
        {
            using var document = JsonDocument.Parse(payload);
            var root = document.RootElement;
            if (root.TryGetProperty("message", out var message))
            {
                return message.GetString();
            }

            if (root.TryGetProperty("error", out var error))
            {
                return error.ValueKind == JsonValueKind.String
                    ? error.GetString()
                    : error.TryGetProperty("message", out var nested) ? nested.GetString() : null;
            }
        }
        catch (JsonException)
        {
            // The status line below is safer and more useful than an HTML error body.
        }

        return null;
    }
}
