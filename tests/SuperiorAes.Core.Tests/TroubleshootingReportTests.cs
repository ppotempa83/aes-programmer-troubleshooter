using SuperiorAes.Core.Diagnostics;
using SuperiorAes.Core.Models;
using SuperiorAes.Core.Reporting;

namespace SuperiorAes.Core.Tests;

public sealed class TroubleshootingReportTests
{
    [Fact]
    public void ReportContainsVisualPathsRouteValuesAndRecommendationsOnly()
    {
        var status = new AesLocalStatus("7788F", "1.2", "1A2B", "2001", 2, "000", 5);
        var routes = new[]
        {
            new RouteEntry(1, "2001", 1, 5, "03"),
            new RouteEntry(2, "2002", 2, 6, "82")
        };
        var findings = DiagnosticEngine.Analyze(status, routes);
        var context = new TroubleshootingReportContext(
            "Test site",
            "Account 42",
            "Technician",
            AesModel.Aes7788F,
            status,
            routes,
            findings);

        var html = TroubleshootingReportGenerator.Generate(context);

        Assert.Contains("<svg", html);
        Assert.Contains("Radio 2001", html);
        Assert.Contains("L01", html);
        Assert.Contains("N5", html);
        Assert.Contains("Q03", html);
        Assert.Contains("L02", html);
        Assert.Contains("N6", html);
        Assert.Contains("Q82", html);
        Assert.Contains("Upstream investigation required", html);
        Assert.Contains("Prioritized technician recommendations", html);
        Assert.DoesNotContain("Session transcript", html);
        Assert.DoesNotContain("Zone status", html);
    }

    [Fact]
    public void ReportExplainsWhenRoutingDataWasNotCaptured()
    {
        var context = new TroubleshootingReportContext(
            "Test site",
            "Account 42",
            "Technician",
            AesModel.Aes7744F,
            null,
            [],
            []);

        var html = TroubleshootingReportGenerator.Generate(context);

        Assert.Contains("No route data captured", html);
        Assert.Contains("Run the baseline diagnosis", html);
        Assert.Contains("Improve route diversity", html);
    }
}
