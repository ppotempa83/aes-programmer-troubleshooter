using SuperiorAes.Core.Diagnostics;
using SuperiorAes.Core.Models;

namespace SuperiorAes.Core.Tests;

public sealed class DiagnosticEngineTests
{
    [Fact]
    public void SeparatesUpstreamMeshProblemFromLocalRfQuality()
    {
        var status = new AesLocalStatus("7788F", "7.8", "1A2B", "AA11", 2, "000", 6);
        RouteEntry[] routes =
        [
            new(1, "AA11", 1, 7, "03"),
            new(2, "BB12", 1, 6, "03")
        ];

        var findings = DiagnosticEngine.Analyze(status, routes);

        Assert.Contains(findings, finding => finding.Title.Contains("upstream mesh", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(findings, finding => finding.Title.Contains("local RF-path", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void FlagsNotEnrolledSubscriber()
    {
        var status = new AesLocalStatus("7744F", "6.0", "1A2B", "XXXX", 255, "000", 7);

        var findings = DiagnosticEngine.Analyze(status, Array.Empty<RouteEntry>());

        Assert.Contains(findings, finding =>
            finding.Severity == DiagnosticSeverity.Critical &&
            finding.Title.Contains("not enrolled", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void SelectsBestSurveyEvidence()
    {
        SiteSurveyTrial[] trials =
        [
            Trial("Inside", 7, "82", 1, 30),
            Trial("Outside", 3, "03", 4, 100)
        ];

        var best = DiagnosticEngine.SelectBestTrial(trials);

        Assert.NotNull(best);
        Assert.Equal("Outside", best.Location);
    }

    private static SiteSurveyTrial Trial(
        string location,
        int netcon,
        string quality,
        int routes,
        decimal ack) =>
        new(DateTimeOffset.Now, location, "Test", "Test", netcon, quality, routes, ack, 2, 0.1m, 13.6m, 13.2m, string.Empty);
}

