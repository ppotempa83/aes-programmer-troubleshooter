using System.Globalization;
using System.Text.Json;
using SuperiorAes.Android.Services;
using SuperiorAes.Core.Diagnostics;
using SuperiorAes.Core.Models;
using SuperiorAes.Core.Protocol;
using SuperiorAes.Core.SiteAnalysis;

namespace SuperiorAes.Android.Pages;

public partial class TroubleshooterPage : ContentPage
{
    private readonly ICompanionSession _session;
    private readonly Emergency24CoverageService _coverage = new();

    public TroubleshooterPage(ICompanionSession session)
    {
        InitializeComponent();
        _session = session;
        TechnicianEntry.Text = _session.TechnicianName;
        ApplyLatestCoverage();
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        ApplyLatestCoverage();
    }

    private async void OnBaselineClicked(object? sender, EventArgs args)
    {
        try
        {
            if (!_session.IsConnected)
            {
                await _session.ConnectAsync();
            }

            AssessmentLabel.Text = "Reading local status…";
            await _session.SendCommandAsync(AesCommand.LocalStatus);
            await Task.Delay(700);
            await _session.SendCommandAsync(AesCommand.RoutingTable);
            await Task.Delay(900);
            await _session.SendCommandAsync(AesCommand.ZoneStatus);
            await Task.Delay(700);
            RefreshAssessment();
        }
        catch (Exception exception) when (exception is InvalidOperationException or IOException or TimeoutException)
        {
            AssessmentLabel.Text = $"Baseline stopped: {exception.Message}";
        }
    }

    private async void OnExportClicked(object? sender, EventArgs args)
    {
        try
        {
            var technician = TechnicianEntry.Text?.Trim() ?? string.Empty;
            if (technician.Length > 0)
            {
                _session.SetTechnicianName(technician);
            }

            var path = await _session.ExportTroubleshootingAsync(
                SiteEntry.Text ?? string.Empty,
                AccountEntry.Text ?? string.Empty,
                technician);
            await Share.Default.RequestAsync(
                new ShareFileRequest("Superior AES troubleshooting report", new ShareFile(path)));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            await DisplayAlertAsync("Export failed", exception.Message, "OK");
        }
    }

    private async void OnCoverageClicked(object? sender, EventArgs args)
    {
        if (!TryReadCoordinates(
                CoverageLatitudeEntry.Text,
                CoverageLongitudeEntry.Text,
                out var latitude,
                out var longitude))
        {
            await DisplayAlertAsync(
                "Invalid coordinates",
                "Latitude must be -90 through 90 and longitude -180 through 180.",
                "OK");
            return;
        }

        CoverageEvidenceLabel.Text = "Loading all Emergency24 AES antenna layers…";
        try
        {
            var coverage = await _coverage.AnalyzeAsync(latitude, longitude);
            _session.RecordCoverageAnalysis(coverage);
            CoverageLatitudeEntry.Text =
                coverage.Latitude.ToString("0.000000", CultureInfo.InvariantCulture);
            CoverageLongitudeEntry.Text =
                coverage.Longitude.ToString("0.000000", CultureInfo.InvariantCulture);
            RefreshCoverageEvidence(coverage);
            if (_session.LastStatus is not null || _session.Routes.Count > 0)
            {
                RefreshAssessment(recordActivity: false);
            }
        }
        catch (Exception exception) when (
            exception is HttpRequestException or IOException or JsonException or TaskCanceledException)
        {
            CoverageEvidenceLabel.Text =
                $"Emergency24 coverage could not be loaded: {exception.Message}";
            _session.RecordActivity("Emergency24 troubleshooter lookup failed");
        }
    }

    private async void OnOpenAesMapClicked(object? sender, EventArgs args)
    {
        await Launcher.Default.OpenAsync(new Uri(Emergency24CoverageService.MapUrl));
        _session.RecordActivity("Emergency24 AES map opened from troubleshooter");
    }

    private void RefreshAssessment(bool recordActivity = true)
    {
        var coverage = _session.LatestCoverage;
        var mappedCoverage = SelectMappedCoverage(coverage);
        var findings = DiagnosticEngine.Analyze(
            _session.LastStatus,
            _session.Routes,
            _session.SurveyTrials,
            mappedCoverage);
        var lines = new List<string>();
        if (_session.LastStatus is { } status)
        {
            lines.Add($"SUBSCRIBER {status.SubscriberId} · {status.Model} · STAT {status.StatCode} · NETCON {status.NetCon}");
        }
        else
        {
            lines.Add("No complete local-status reply captured.");
        }

        lines.Add("PATH");
        if (_session.Routes.Count == 0)
        {
            lines.Add("Subscriber - - - > no parsed route");
        }
        else
        {
            lines.AddRange(_session.Routes.Select(route =>
                $"Subscriber ──P{route.Preference}──> {route.Id} ──> AES mesh · L{route.LinkLayer:00}/N{route.PeerNetCon}/Q{route.Quality} {route.QualityLabel}"));
        }

        if (coverage is not null)
        {
            lines.Add(string.Empty);
            lines.Add("EMERGENCY24 SITE EVIDENCE");
            lines.Add(
                $"Coordinates {coverage.Latitude:0.000000}, {coverage.Longitude:0.000000} · " +
                coverage.RecommendationSummary);
            lines.AddRange(coverage.Results.Select(result =>
                $"{result.Antenna} ({result.GainDb:0.#} dB) · " +
                $"{(result.ExpectedNetCon.HasValue ? $"expected N{result.ExpectedNetCon}" : "no mapped signal")} · " +
                $"N5 peers {result.NetConFivePeers} · N6/7 peers {result.NetConSixOrSevenPeers}"));
            if (_session.LastStatus is { } measured && mappedCoverage is not null)
            {
                lines.Add(
                    $"Measured subscriber N{measured.NetCon} compared with " +
                    $"{mappedCoverage.Antenna} mapped " +
                    $"{(mappedCoverage.ExpectedNetCon.HasValue ? $"N{mappedCoverage.ExpectedNetCon}" : "no-signal evidence")}.");
            }
        }

        lines.Add(string.Empty);
        lines.Add("FINDINGS / RECOMMENDATIONS");
        lines.AddRange(findings.Select(finding =>
            $"{finding.Severity.ToString().ToUpperInvariant()} · {finding.Title}\n  {finding.Detail}\n  ACTION: {finding.RecommendedAction}"));
        AssessmentLabel.Text = string.Join(Environment.NewLine, lines);
        if (recordActivity)
        {
            _session.RecordActivity($"Troubleshooting baseline evaluated · {findings.Count} finding(s)");
        }
    }

    private void ApplyLatestCoverage()
    {
        if (_session.LatestCoverage is not { } coverage)
        {
            return;
        }

        CoverageLatitudeEntry.Text =
            coverage.Latitude.ToString("0.000000", CultureInfo.InvariantCulture);
        CoverageLongitudeEntry.Text =
            coverage.Longitude.ToString("0.000000", CultureInfo.InvariantCulture);
        RefreshCoverageEvidence(coverage);
    }

    private void RefreshCoverageEvidence(AesMapCoverageAnalysis coverage)
    {
        CoverageEvidenceLabel.Text = string.Join(
            Environment.NewLine,
            new[]
            {
                $"Coordinates {coverage.Latitude:0.000000}, {coverage.Longitude:0.000000}",
                coverage.RecommendationSummary
            }.Concat(coverage.Results.Select(result => result.Summary)));
    }

    private static AesMapCoverageResult? SelectMappedCoverage(
        AesMapCoverageAnalysis? coverage) =>
        coverage?.Recommended ??
        coverage?.Results
            .Where(result => result.ExpectedNetCon.HasValue)
            .OrderBy(result => result.ExpectedNetCon)
            .ThenBy(result => result.GainDb)
            .FirstOrDefault() ??
        coverage?.Results.LastOrDefault();

    private static bool TryReadCoordinates(
        string? latitudeText,
        string? longitudeText,
        out double latitude,
        out double longitude)
    {
        latitude = 0;
        longitude = 0;
        if (!TryDouble(latitudeText, out var parsedLatitude) ||
            !TryDouble(longitudeText, out var parsedLongitude))
        {
            return false;
        }

        latitude = parsedLatitude;
        longitude = parsedLongitude;
        return latitude is >= -90 and <= 90 &&
               longitude is >= -180 and <= 180;
    }

    private static bool TryDouble(string? value, out double result) =>
        double.TryParse(
            value,
            NumberStyles.Float,
            CultureInfo.InvariantCulture,
            out result) ||
        double.TryParse(
            value,
            NumberStyles.Float,
            CultureInfo.CurrentCulture,
            out result);
}
