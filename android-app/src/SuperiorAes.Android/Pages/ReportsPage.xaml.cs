using System.Text;
using SuperiorAes.Android.Services;
using SuperiorAes.Core.Diagnostics;
using SuperiorAes.Core.Models;
using SuperiorAes.Core.Reporting;

namespace SuperiorAes.Android.Pages;

public partial class ReportsPage : ContentPage
{
    private readonly ICompanionSession _session;

    public ReportsPage(ICompanionSession session)
    {
        InitializeComponent();
        _session = session;
        TechnicianEntry.Text = _session.TechnicianName;
        RefreshMetadata();
        _session.StateChanged += OnSessionChanged;
    }

    private void OnSessionChanged(object? sender, EventArgs args) =>
        MainThread.BeginInvokeOnMainThread(RefreshMetadata);

    private async void OnSessionExportClicked(object? sender, EventArgs args)
    {
        try
        {
            UpdateTechnician();
            var export = await _session.ExportSessionAsync();
            ExportOutputLabel.Text =
                $"Created {Path.GetFileName(export.TextPath)}\n" +
                $"Created {Path.GetFileName(export.SpreadsheetPath)}";
            await Share.Default.RequestAsync(
                new ShareMultipleFilesRequest
                {
                    Title = "Superior AES session text and spreadsheet",
                    Files =
                    [
                        new ShareFile(export.TextPath),
                        new ShareFile(export.SpreadsheetPath)
                    ]
                });
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            ExportOutputLabel.Text = $"Session export failed: {exception.Message}";
        }
    }

    private async void OnFieldReportClicked(object? sender, EventArgs args)
    {
        try
        {
            UpdateTechnician();
            var findings = DiagnosticEngine.Analyze(
                _session.LastStatus,
                _session.Routes,
                _session.SurveyTrials,
                SelectMappedCoverage(_session.LatestCoverage));
            var context = new ReportContext(
                Value(SiteEntry.Text, "Site not entered"),
                Value(AccountEntry.Text, "Account not entered"),
                Value(TechnicianEntry.Text, Environment.UserName),
                _session.SelectedModel == "7744F" ? AesModel.Aes7744F : AesModel.Aes7788F,
                _session.LastStatus,
                _session.Routes,
                _session.Zones,
                findings,
                _session.SurveyTrials,
                _session.Transcript,
                _session.LatestCoverage,
                _session.LatestBuilding,
                _session.LatestRadioRecommendation);
            var directory = Path.Combine(FileSystem.Current.AppDataDirectory, "Exports");
            Directory.CreateDirectory(directory);
            var path = Path.Combine(
                directory,
                $"Superior-AES-Field-Report-{DateTimeOffset.Now.LocalDateTime:yyyyMMdd-HHmmss}.html");
            await File.WriteAllTextAsync(
                path,
                HtmlReportGenerator.Generate(context),
                new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            _session.RecordActivity($"Printable field report created · {Path.GetFileName(path)}");
            ExportOutputLabel.Text = $"Created {Path.GetFileName(path)}";
            await Share.Default.RequestAsync(
                new ShareFileRequest("Superior AES printable field report", new ShareFile(path)));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            ExportOutputLabel.Text = $"Field report failed: {exception.Message}";
        }
    }

    private void UpdateTechnician()
    {
        var value = TechnicianEntry.Text?.Trim() ?? string.Empty;
        if (value.Length > 0 && value != _session.TechnicianName)
        {
            _session.SetTechnicianName(value);
        }
    }

    private void RefreshMetadata()
    {
        MetadataLabel.Text =
            $"Session: {_session.SessionId:D}\n" +
            $"Started: {_session.SessionStarted.LocalDateTime:[MM-dd-yyyy / hh:mm (tt)]}\n" +
            $"Subscriber: {Value(_session.SubscriberId, "not read")}\n" +
            $"Model: {_session.SelectedModel}\n" +
            $"Connection: {(_session.IsConnected ? _session.ConnectionName : "disconnected")}\n" +
            $"Terminal entries: {_session.Entries.Count}";
    }

    private static string Value(string? value, string fallback)
    {
        var normalized = value?.Trim().Replace("\r", " ").Replace("\n", " ");
        return string.IsNullOrWhiteSpace(normalized) ? fallback : normalized;
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
}
