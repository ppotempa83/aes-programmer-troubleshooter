namespace SuperiorAes.Core.Models;

public enum AesModel
{
    Aes7744F,
    Aes7788F
}

public enum DiagnosticSeverity
{
    Pass,
    Information,
    Warning,
    Critical
}

public sealed record AesLocalStatus(
    string Model,
    string Firmware,
    string SubscriberId,
    string RouteOne,
    int Level,
    string StatCode,
    int NetCon)
{
    public bool IsEnrolled =>
        !string.Equals(RouteOne, "XXXX", StringComparison.OrdinalIgnoreCase) &&
        Level != 255;
}

public sealed record RouteEntry(
    int Preference,
    string Id,
    int LinkLayer,
    int PeerNetCon,
    string Quality)
{
    public int RepetitionsDecoded =>
        Quality.Length == 2 && char.IsDigit(Quality[1]) ? Quality[1] - '0' : 0;

    public bool CarrierDetected =>
        Quality.Length == 2 && Quality[0] == '0';

    public string QualityLabel => Quality switch
    {
        "03" => "Best",
        "02" => "Good",
        "01" => "Degraded",
        "83" => "Weak carrier / readable",
        "82" => "Marginal",
        "81" => "Poor",
        _ => CarrierDetected ? "Carrier detected" : "Unknown / weak carrier"
    };
}

public sealed record ZoneState(int Zone, char State)
{
    public string Label => State switch
    {
        '0' => "Normal",
        '1' => "Alarm / Fault",
        'T' => "Trouble",
        _ => "Unknown"
    };
}

public sealed record StatFault(string Code, string Name, string RecommendedAction);

public sealed record DiagnosticFinding(
    DiagnosticSeverity Severity,
    string Title,
    string Detail,
    string RecommendedAction);

public sealed record SiteSurveyTrial(
    DateTimeOffset RecordedAt,
    string Location,
    string Antenna,
    string Cable,
    int NetCon,
    string BestQuality,
    int RouteCount,
    decimal AckSuccessPercent,
    decimal ForwardPowerWatts,
    decimal ReflectedPowerWatts,
    decimal DcVoltageIdle,
    decimal DcVoltageKeyed,
    string Notes)
{
    public decimal ReflectedPowerPercent =>
        ForwardPowerWatts <= 0
            ? 0
            : Math.Round(ReflectedPowerWatts / ForwardPowerWatts * 100m, 1);

    public decimal VoltageDrop =>
        Math.Max(0, DcVoltageIdle - DcVoltageKeyed);
}

public sealed record ReportContext(
    string SiteName,
    string AccountNumber,
    string Technician,
    AesModel SelectedModel,
    AesLocalStatus? Status,
    IReadOnlyList<RouteEntry> Routes,
    IReadOnlyList<ZoneState> Zones,
    IReadOnlyList<DiagnosticFinding> Findings,
    IReadOnlyList<SiteSurveyTrial> SurveyTrials,
    string Transcript,
    AesMapCoverageAnalysis? Coverage = null,
    BuildingSiteData? Building = null,
    RadioSiteRecommendation? RadioRecommendation = null);
