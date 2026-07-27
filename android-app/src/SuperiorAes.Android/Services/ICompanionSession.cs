using SuperiorAes.Android.Models;
using SuperiorAes.Core.Models;
using SuperiorAes.Core.Protocol;

namespace SuperiorAes.Android.Services;

public sealed record GuidedResponse(string Value, string DisplayValue, bool IsSensitive = false);

public sealed record SessionExportResult(string TextPath, string SpreadsheetPath);

public interface ICompanionSession
{
    event EventHandler? StateChanged;

    Guid SessionId { get; }
    DateTimeOffset SessionStarted { get; }
    string SelectedModel { get; }
    bool IsConnected { get; }
    string ConnectionName { get; }
    AesTransportMode TransportMode { get; }
    bool IsBusy { get; }
    string BusyMessage { get; }
    string SubscriberId { get; }
    string TechnicianName { get; }
    AesLocalStatus? LastStatus { get; }
    AesMapCoverageAnalysis? LatestCoverage { get; }
    BuildingSiteData? LatestBuilding { get; }
    RadioSiteRecommendation? LatestRadioRecommendation { get; }
    IReadOnlyList<RouteEntry> Routes { get; }
    IReadOnlyList<ZoneState> Zones { get; }
    IReadOnlyList<SiteSurveyTrial> SurveyTrials { get; }
    IReadOnlyList<SessionLogEntry> Entries { get; }
    string Transcript { get; }

    void SelectModel(string model);
    void SetTechnicianName(string value);
    void SetSubscriberId(string value);
    void AddSurveyTrial(SiteSurveyTrial trial);
    void RecordCoverageAnalysis(AesMapCoverageAnalysis coverage);
    void RecordSiteAnalysis(
        AesMapCoverageAnalysis coverage,
        BuildingSiteData? building,
        RadioSiteRecommendation? recommendation);
    void RecordActivity(string action);
    void RegisterSensitiveValue(string value);
    Task SelectTransportModeAsync(
        AesTransportMode mode,
        bool hardwareSafetyWarningAccepted,
        CancellationToken cancellationToken = default);
    Task ConnectAsync(CancellationToken cancellationToken = default);
    Task DisconnectAsync(CancellationToken cancellationToken = default);
    Task SendCommandAsync(AesCommand command, CancellationToken cancellationToken = default);
    Task RunConversationAsync(
        AesCommand command,
        IReadOnlyList<GuidedResponse> responses,
        CancellationToken cancellationToken = default);
    Task RunGuidedActionAsync(
        string action,
        string result,
        CancellationToken cancellationToken = default);
    Task SendRawAsync(string value, CancellationToken cancellationToken = default);
    Task SendEnterAsync(CancellationToken cancellationToken = default);
    Task SendEscapeAsync(CancellationToken cancellationToken = default);
    Task<SessionExportResult> ExportSessionAsync(CancellationToken cancellationToken = default);
    Task<string> ExportTroubleshootingAsync(
        string siteName,
        string accountNumber,
        string technician,
        CancellationToken cancellationToken = default);
}
