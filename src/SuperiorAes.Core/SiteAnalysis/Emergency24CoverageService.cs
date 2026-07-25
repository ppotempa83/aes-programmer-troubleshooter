using System.Collections.Concurrent;
using System.Globalization;
using System.Text.Json;
using SuperiorAes.Core.Models;

namespace SuperiorAes.Core.SiteAnalysis;

public sealed class Emergency24CoverageService
{
    public const string MapUrl = "https://www.emergency24.com/maps/";
    private const string BaseUrl = "https://www.emergency24.com/maps/";
    private readonly HttpClient _httpClient;
    private readonly ConcurrentDictionary<string, Lazy<Task<IReadOnlyList<CoverageCell>>>> _cache = new();

    private static readonly IReadOnlyList<(string Name, decimal Gain, string File)> Layers =
    [
        ("Rubber Duck", 2.5m, "mapdatard.json"),
        ("3 dB", 3m, "mapdata3db.json"),
        ("5 dB", 5m, "mapdata5db.json"),
        ("6 dB", 6m, "mapdata6db.json")
    ];

    public Emergency24CoverageService(HttpClient? httpClient = null)
    {
        _httpClient = httpClient ?? new HttpClient { Timeout = TimeSpan.FromSeconds(20) };
    }

    public async Task<AesMapCoverageAnalysis> AnalyzeAsync(
        double latitude,
        double longitude,
        CancellationToken cancellationToken = default)
    {
        ValidateCoordinates(latitude, longitude);
        var tasks = Layers.Select(layer => LookupLayerAsync(layer, latitude, longitude, cancellationToken));
        var results = await Task.WhenAll(tasks);
        var recommended = results
            .Where(result => result.ExpectedNetCon is <= 5)
            .OrderBy(result => result.GainDb)
            .ThenBy(result => result.ExpectedNetCon)
            .FirstOrDefault();

        return new AesMapCoverageAnalysis(latitude, longitude, results, recommended);
    }

    private async Task<AesMapCoverageResult> LookupLayerAsync(
        (string Name, decimal Gain, string File) layer,
        double latitude,
        double longitude,
        CancellationToken cancellationToken)
    {
        var cells = await _cache.GetOrAdd(
            layer.File,
            file => new Lazy<Task<IReadOnlyList<CoverageCell>>>(
                () => DownloadLayerAsync(file, CancellationToken.None))).Value.WaitAsync(cancellationToken);

        var cell = cells.FirstOrDefault(value =>
            longitude >= value.MinLongitude &&
            longitude <= value.MaxLongitude &&
            latitude >= value.MinLatitude &&
            latitude <= value.MaxLatitude &&
            PointInPolygon(longitude, latitude, value.Rings));

        return new AesMapCoverageResult(
            layer.Name,
            layer.Gain,
            cell?.ExpectedNetCon,
            cell?.NetConFivePeers ?? 0,
            cell?.NetConSixOrSevenPeers ?? 0,
            latitude,
            longitude,
            MapUrl);
    }

    private async Task<IReadOnlyList<CoverageCell>> DownloadLayerAsync(
        string file,
        CancellationToken cancellationToken)
    {
        await using var stream = await _httpClient.GetStreamAsync(BaseUrl + file, cancellationToken);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
        var cells = new List<CoverageCell>();

        foreach (var feature in document.RootElement.GetProperty("features").EnumerateArray())
        {
            var properties = feature.GetProperty("properties");
            var geometry = feature.GetProperty("geometry");
            if (!string.Equals(geometry.GetProperty("type").GetString(), "Polygon", StringComparison.Ordinal))
            {
                continue;
            }

            var rings = geometry.GetProperty("coordinates")
                .EnumerateArray()
                .Select(ring => ring.EnumerateArray()
                    .Select(point => (
                        Longitude: point[0].GetDouble(),
                        Latitude: point[1].GetDouble()))
                    .ToArray())
                .Where(ring => ring.Length >= 3)
                .ToArray();
            if (rings.Length == 0)
            {
                continue;
            }

            var points = rings.SelectMany(ring => ring).ToArray();
            cells.Add(new CoverageCell(
                ParseNullableInt(properties, "netcon"),
                ParseInt(properties, "numberOfFive"),
                ParseInt(properties, "numberOfSix"),
                rings,
                points.Min(point => point.Longitude),
                points.Max(point => point.Longitude),
                points.Min(point => point.Latitude),
                points.Max(point => point.Latitude)));
        }

        return cells;
    }

    private static bool PointInPolygon(
        double longitude,
        double latitude,
        IReadOnlyList<(double Longitude, double Latitude)[]> rings)
    {
        if (!PointInRing(longitude, latitude, rings[0]))
        {
            return false;
        }

        return rings.Skip(1).All(ring => !PointInRing(longitude, latitude, ring));
    }

    private static bool PointInRing(
        double longitude,
        double latitude,
        IReadOnlyList<(double Longitude, double Latitude)> ring)
    {
        var inside = false;
        for (int current = 0, previous = ring.Count - 1; current < ring.Count; previous = current++)
        {
            var a = ring[current];
            var b = ring[previous];
            var crosses = (a.Latitude > latitude) != (b.Latitude > latitude) &&
                          longitude < (b.Longitude - a.Longitude) *
                          (latitude - a.Latitude) /
                          (b.Latitude - a.Latitude) + a.Longitude;
            if (crosses)
            {
                inside = !inside;
            }
        }

        return inside;
    }

    private static int ParseInt(JsonElement element, string name) =>
        ParseNullableInt(element, name) ?? 0;

    private static int? ParseNullableInt(JsonElement element, string name)
    {
        if (!element.TryGetProperty(name, out var value))
        {
            return null;
        }

        return value.ValueKind == JsonValueKind.Number
            ? value.GetInt32()
            : int.TryParse(value.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
                ? parsed
                : null;
    }

    private static void ValidateCoordinates(double latitude, double longitude)
    {
        if (latitude is < -90 or > 90 || longitude is < -180 or > 180)
        {
            throw new ArgumentOutOfRangeException(nameof(latitude), "Latitude or longitude is outside its valid range.");
        }
    }

    private sealed record CoverageCell(
        int? ExpectedNetCon,
        int NetConFivePeers,
        int NetConSixOrSevenPeers,
        IReadOnlyList<(double Longitude, double Latitude)[]> Rings,
        double MinLongitude,
        double MaxLongitude,
        double MinLatitude,
        double MaxLatitude);
}
