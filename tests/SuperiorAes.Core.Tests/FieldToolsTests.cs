using SuperiorAes.Core.Diagnostics;
using SuperiorAes.Core.Models;
using SuperiorAes.Core.Simulation;
using SuperiorAes.Core.SiteAnalysis;
using SuperiorAes.Core.Templates;
using System.Net;
using System.Text;

namespace SuperiorAes.Core.Tests;

public sealed class FieldToolsTests
{
    [Fact]
    public void VirtualMeshCalculatesZeroChanceWhenEitherRadioIsOffline()
    {
        var source = new VirtualRadio { RadioId = "1001", Online = true, Quality = "03", NetCon = 3 };
        var target = new VirtualRadio { RadioId = "1002", Online = false, Quality = "03", NetCon = 3 };

        Assert.Equal(0, VirtualMeshSimulator.CalculateSuccessPercent(source, target));
    }

    [Fact]
    public void VirtualMeshEmitsOneEventForEachBroadcastTarget()
    {
        var radios = new[]
        {
            new VirtualRadio { RadioId = "1001", Quality = "03", NetCon = 3 },
            new VirtualRadio { RadioId = "1002", Quality = "02", NetCon = 4 },
            new VirtualRadio { RadioId = "1003", Quality = "01", NetCon = 5 }
        };
        var simulator = new VirtualMeshSimulator(randomSeed: 12);

        var events = simulator.Send(radios, "1001", "BROADCAST", "Fire alarm");

        Assert.Equal(2, events.Count);
        Assert.All(events, meshEvent => Assert.Equal("1001", meshEvent.SourceId));
        Assert.All(events, meshEvent => Assert.Equal("Fire alarm", meshEvent.SignalType));
    }

    [Fact]
    public async Task ProgrammingTemplatesRoundTripWithoutCipherData()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"SuperiorAes-{Guid.NewGuid():N}");
        var path = Path.Combine(directory, "templates.json");
        var store = new ProgrammingTemplateStore(path);
        var expected = ProgrammingTemplate.Defaults[0] with { Name = "Test template", SubscriberId = "1A2B" };

        try
        {
            await store.SaveAsync([expected]);
            var loaded = await store.LoadAsync();

            Assert.Single(loaded);
            Assert.Equal(expected, loaded[0]);
            Assert.DoesNotContain("cipher", await File.ReadAllTextAsync(path), StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    [Fact]
    public void DiagnosticEngineComparesMeasuredNetConToMappedExpectation()
    {
        var status = new AesLocalStatus("7788F", "1.0", "1234", "2001", 1, "000", 7);
        var routes = new[]
        {
            new RouteEntry(1, "2001", 1, 5, "03"),
            new RouteEntry(2, "2002", 2, 5, "02")
        };
        var mapped = new AesMapCoverageResult(
            "3 dB",
            3,
            5,
            2,
            0,
            41.88,
            -87.63,
            Emergency24CoverageService.MapUrl);

        var findings = DiagnosticEngine.Analyze(status, routes, mappedCoverage: mapped);

        Assert.Contains(findings, finding =>
            finding.Title.Contains("worse than mapped", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void RecommendationEscalatesLowGainForMetalConstruction()
    {
        var mapped = new AesMapCoverageResult(
            "Rubber Duck",
            2.5m,
            5,
            3,
            0,
            41.88,
            -87.63,
            Emergency24CoverageService.MapUrl);
        var coverage = new AesMapCoverageAnalysis(41.88, -87.63, [mapped], mapped);

        var recommendation = RadioRecommendationEngine.Recommend(
            coverage,
            null,
            "Steel / metal construction",
            "Automatic — based on evidence");

        Assert.Contains("5 dB", recommendation.Antenna, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Exterior", recommendation.Location, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Emergency24ParserFindsPointAndChoosesLowestPassingLayer()
    {
        const string geoJson =
            """
            {
              "type": "FeatureCollection",
              "features": [{
                "type": "Feature",
                "properties": {
                  "antenna": "test",
                  "numberOfFive": "4",
                  "numberOfSix": "1",
                  "netcon": "5"
                },
                "geometry": {
                  "type": "Polygon",
                  "coordinates": [[[-88.0,41.0],[-87.0,41.0],[-87.0,42.0],[-88.0,42.0],[-88.0,41.0]]]
                }
              }]
            }
            """;
        using var httpClient = new HttpClient(new StaticResponseHandler(geoJson));
        var service = new Emergency24CoverageService(httpClient);

        var result = await service.AnalyzeAsync(41.5, -87.5);

        Assert.Equal(4, result.Results.Count);
        Assert.Equal("Rubber Duck", result.Recommended?.Antenna);
        Assert.All(result.Results, layer => Assert.Equal(5, layer.ExpectedNetCon));
        Assert.All(result.Results, layer => Assert.Equal(4, layer.NetConFivePeers));
    }

    private sealed class StaticResponseHandler(string payload) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(payload, Encoding.UTF8, "application/json")
            });
    }
}
