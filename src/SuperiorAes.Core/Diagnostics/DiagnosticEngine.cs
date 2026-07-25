using SuperiorAes.Core.Models;
using SuperiorAes.Core.Protocol;

namespace SuperiorAes.Core.Diagnostics;

public static class DiagnosticEngine
{
    public static IReadOnlyList<DiagnosticFinding> Analyze(
        AesLocalStatus? status,
        IReadOnlyList<RouteEntry> routes,
        IReadOnlyList<SiteSurveyTrial>? trials = null,
        AesMapCoverageResult? mappedCoverage = null)
    {
        var findings = new List<DiagnosticFinding>();

        if (status is null)
        {
            findings.Add(new DiagnosticFinding(
                DiagnosticSeverity.Critical,
                "No readable local status",
                "The app has not received a complete SUB / ID / RT1 / STAT / NETCON response.",
                "Check the selected COM port, cable wiring, 4800-8-N-1 settings, and that RTS/CTS flow control is off."));
            return findings;
        }

        AddStatFindings(findings, status);
        AddEnrollmentFindings(findings, status);
        AddNetConFindings(findings, status);
        AddRouteFindings(findings, status, routes);
        AddMappedCoverageFindings(findings, status, mappedCoverage);

        if (trials is { Count: > 0 })
        {
            AddSurveyFindings(findings, trials);
        }

        if (findings.All(finding => finding.Severity is DiagnosticSeverity.Pass or DiagnosticSeverity.Information))
        {
            findings.Insert(0, new DiagnosticFinding(
                DiagnosticSeverity.Pass,
                "Baseline checks passed",
                "The available status and route data do not show an immediate enrollment, hardware, or mesh-path failure.",
                "Complete alarm, trouble, supervisory, restoral, and central-station receipt tests before returning the account to service."));
        }

        return findings;
    }

    public static SiteSurveyTrial? SelectBestTrial(IReadOnlyList<SiteSurveyTrial> trials) =>
        trials
            .OrderBy(trial => trial.NetCon)
            .ThenByDescending(trial => QualityScore(trial.BestQuality))
            .ThenByDescending(trial => trial.AckSuccessPercent)
            .ThenBy(trial => trial.ReflectedPowerPercent)
            .FirstOrDefault();

    public static int QualityScore(string quality) =>
        quality.ToUpperInvariant() switch
        {
            "03" => 6,
            "02" => 5,
            "01" => 4,
            "83" => 3,
            "82" => 2,
            "81" => 1,
            _ => 0
        };

    private static void AddStatFindings(List<DiagnosticFinding> findings, AesLocalStatus status)
    {
        var faults = AesParsers.DecodeStat(status.StatCode);
        if (faults.Count == 0)
        {
            findings.Add(new DiagnosticFinding(
                DiagnosticSeverity.Pass,
                "Self-test status is clear",
                $"STAT {status.StatCode} does not contain a decoded fault.",
                "Continue with network and signal-path testing."));
            return;
        }

        findings.AddRange(faults.Select(fault => new DiagnosticFinding(
            fault.Code is "004" or "008" or "080"
                ? DiagnosticSeverity.Critical
                : DiagnosticSeverity.Warning,
            $"{fault.Code} — {fault.Name}",
            $"The subscriber reports STAT {status.StatCode}.",
            fault.RecommendedAction)));
    }

    private static void AddEnrollmentFindings(List<DiagnosticFinding> findings, AesLocalStatus status)
    {
        if (status.IsEnrolled)
        {
            findings.Add(new DiagnosticFinding(
                DiagnosticSeverity.Pass,
                "Subscriber is enrolled",
                $"RT1 is {status.RouteOne} and LEVEL is {status.Level}.",
                "Verify route quality and two-way acknowledgement behavior."));
            return;
        }

        findings.Add(new DiagnosticFinding(
            DiagnosticSeverity.Critical,
            "Subscriber is not enrolled",
            $"RT1 is {status.RouteOne} and LEVEL is {status.Level}.",
            "Verify the subscriber ID, network cipher, operating frequency, antenna/coax path, and that compatible AES traffic is present; then use the physical board RESET button."));
    }

    private static void AddNetConFindings(List<DiagnosticFinding> findings, AesLocalStatus status)
    {
        var finding = status.NetCon switch
        {
            <= 5 => new DiagnosticFinding(
                DiagnosticSeverity.Pass,
                $"NETCON {status.NetCon} is within the fire target",
                "The legacy AES fire manuals identify 0–5 as the permitted range.",
                "Preserve route diversity and complete end-to-end signal testing."),
            6 => new DiagnosticFinding(
                DiagnosticSeverity.Warning,
                "NETCON 6 requires corrective investigation",
                "Connectivity or route diversity is degraded.",
                "Compare route Q values and peer N/L values, then run controlled antenna-location and acknowledgement tests."),
            _ => new DiagnosticFinding(
                DiagnosticSeverity.Critical,
                "NETCON 7 is unacceptable",
                "The subscriber does not have a satisfactory network path.",
                "Do not return the account to normal service until enrollment, RF path, and upstream mesh health are corrected.")
        };

        findings.Add(finding);
    }

    private static void AddRouteFindings(
        List<DiagnosticFinding> findings,
        AesLocalStatus status,
        IReadOnlyList<RouteEntry> routes)
    {
        if (routes.Count == 0)
        {
            findings.Add(new DiagnosticFinding(
                DiagnosticSeverity.Critical,
                "No routing table entries captured",
                "The subscriber returned no parseable Ctrl+T route entries.",
                "Read the routing table again. If it remains empty, troubleshoot enrollment and RF reception."));
            return;
        }

        if (routes.Count == 1)
        {
            findings.Add(new DiagnosticFinding(
                DiagnosticSeverity.Warning,
                "No route diversity",
                $"Only route {routes[0].Id} is available.",
                "Test alternate antenna locations and investigate whether additional healthy peers should be reachable."));
        }
        else
        {
            findings.Add(new DiagnosticFinding(
                DiagnosticSeverity.Pass,
                $"{routes.Count} routing paths captured",
                $"The current preferred route is {routes.OrderBy(route => route.Preference).First().Id}.",
                "Confirm that multiple routes remain stable during the observation period."));
        }

        var strongLocal = routes.Count(route => route.Quality is "03" or "02");
        var unhealthyPeers = routes.Count(route => route.PeerNetCon >= 6);
        var weakLocal = routes.Count(route => route.Quality is "81" or "82" or "83");

        if (strongLocal > 0 && unhealthyPeers > 0 && status.NetCon >= 6)
        {
            findings.Add(new DiagnosticFinding(
                DiagnosticSeverity.Warning,
                "Likely upstream mesh limitation",
                $"{strongLocal} route(s) have good local Q, but {unhealthyPeers} peer(s) report N6/N7.",
                "Escalate the peer or IP-Link topology issue; additional antenna gain at this subscriber may not correct the upstream path."));
        }
        else if (weakLocal > 0 && status.NetCon >= 6)
        {
            findings.Add(new DiagnosticFinding(
                DiagnosticSeverity.Warning,
                "Likely local RF-path weakness",
                $"{weakLocal} route(s) report Q81–Q83 while NETCON is {status.NetCon}.",
                "Inspect antenna, connectors, 50-ohm coax, building attenuation, interference, and subscriber supply voltage under transmit load."));
        }
    }

    private static void AddSurveyFindings(
        List<DiagnosticFinding> findings,
        IReadOnlyList<SiteSurveyTrial> trials)
    {
        var best = SelectBestTrial(trials);
        if (best is null)
        {
            return;
        }

        findings.Add(new DiagnosticFinding(
            DiagnosticSeverity.Information,
            $"Best survey trial: {best.Location}",
            $"NETCON {best.NetCon}, Q{best.BestQuality}, {best.RouteCount} routes, ACK {best.AckSuccessPercent:0.#}%.",
            "Use this as the evidence-based reference configuration, subject to listed installation and mounting requirements."));

        foreach (var trial in trials.Where(trial => trial.ForwardPowerWatts > 0 && trial.ReflectedPowerPercent > 10))
        {
            findings.Add(new DiagnosticFinding(
                DiagnosticSeverity.Critical,
                $"High reflected power at {trial.Location}",
                $"{trial.ReflectedPowerPercent:0.#}% reflected ({trial.ReflectedPowerWatts:0.00} W of {trial.ForwardPowerWatts:0.00} W forward).",
                "Stop transmitter testing and inspect the antenna, coax, connectors, frequency compatibility, and test setup."));
        }
    }

    private static void AddMappedCoverageFindings(
        List<DiagnosticFinding> findings,
        AesLocalStatus status,
        AesMapCoverageResult? mappedCoverage)
    {
        if (mappedCoverage is null)
        {
            return;
        }

        if (!mappedCoverage.HasCoverage)
        {
            findings.Add(new DiagnosticFinding(
                DiagnosticSeverity.Warning,
                "Emergency24 map predicts no signal",
                $"{mappedCoverage.Antenna} has no mapped coverage at {mappedCoverage.Latitude:0.00000}, {mappedCoverage.Longitude:0.00000}.",
                "Treat external mesh coverage as a troubleshooting variable. Confirm the address point, compare other antenna layers, and validate with an on-site survey."));
            return;
        }

        var expected = mappedCoverage.ExpectedNetCon!.Value;
        if (status.NetCon >= 6 && expected <= 5)
        {
            findings.Add(new DiagnosticFinding(
                DiagnosticSeverity.Warning,
                "Measured NETCON is worse than mapped expectation",
                $"Emergency24 predicts NETCON {expected} for {mappedCoverage.Antenna}, but the subscriber reports NETCON {status.NetCon}.",
                "Prioritize local antenna/coax, building attenuation, interference, power-under-transmit, and subscriber configuration before assuming an area-wide mesh limitation."));
            return;
        }

        if (expected >= 6)
        {
            findings.Add(new DiagnosticFinding(
                DiagnosticSeverity.Information,
                "Map evidence supports an external coverage limitation",
                $"Emergency24 predicts NETCON {expected}; nearby mapped peers include {mappedCoverage.NetConFivePeers} N5 and {mappedCoverage.NetConSixOrSevenPeers} N6/7.",
                "Use this as one variable—not a pass/fail result—and compare other antenna layers plus measured route/Q/ACK evidence."));
            return;
        }

        findings.Add(new DiagnosticFinding(
            DiagnosticSeverity.Information,
            "Map expectation recorded",
            $"Emergency24 predicts NETCON {expected} for {mappedCoverage.Antenna}; nearby mapped peers include {mappedCoverage.NetConFivePeers} N5 and {mappedCoverage.NetConSixOrSevenPeers} N6/7.",
            "Confirm the prediction with controlled on-site route, quality, and acknowledgement testing."));
    }
}
