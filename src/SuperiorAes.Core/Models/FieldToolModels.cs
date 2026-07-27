namespace SuperiorAes.Core.Models;

public sealed record AntennaOption(
    string Name,
    string PartNumber,
    decimal GainDb,
    string Application,
    string ImageFile,
    string ProductUrl = "")
{
    public string DisplayName => $"{GainDb:0.#} dB — {Name} ({PartNumber})";
}

public static class AntennaCatalog
{
    public static IReadOnlyList<AntennaOption> All { get; } =
    [
        new("Tamper-resistant rubber duck", "7214", 2.5m, "Indoor", "aes-7214-rubber-duck.png", "https://aes-corp.com/product/7214-2-5-omnidirectional-rubber-duck-antenna/"),
        new("Omnidirectional stealth", "7211", 3m, "Indoor", "aes-7211-stealth.png", "https://aes-corp.com/product/7211-3-db-omnidirectional-stealth-antenna/"),
        new("Omnidirectional stainless", "7210-3", 3m, "Indoor / outdoor", "aes-7210-3.png", "https://aes-corp.com/product/7210-3-3db-omnidirectional-antenna/"),
        new("High-gain stainless", "7210-5", 5m, "Indoor / outdoor", "aes-7210-5.png", "https://aes-corp.com/product/7210-5-5db-omnidirectional-antenna/"),
        new("High-gain fiberglass", "7210-6", 6m, "Indoor / outdoor", "aes-7210-6.png", "https://aes-corp.com/product/7210-6-6db-omnidirectional-antenna/"),
        new("Higher-gain fiberglass", "7210-7", 7m, "Outdoor; 460–470 MHz only", "aes-7210-7.jpg", "https://aes-corp.com/product/7210-7-7db-omnidirectional-antenna/"),
        new("Central-station fiberglass", "7210-9", 9m, "Special application / central station", "aes-7210-9.jpg", "https://aes-corp.com/product/7210-9-9db-omnidirectional-antenna/")
    ];
}

public sealed record ProgrammingTemplate(
    string Name,
    AesModel Model,
    string SubscriberId,
    int CheckInHours,
    int CheckInMinutes,
    string AcReportDelay,
    int ReportDelaySeconds,
    bool FireTroubleEnabled,
    string ZoneConfiguration,
    string RestoralConfiguration,
    bool RepeatingEnabled,
    bool SuppressAcFailure,
    bool SuppressChargerFault,
    bool SuppressGroundFault,
    string Antenna,
    string Notes = "",
    string DialerCaptureModule = "None",
    string ContactIdReportFormat = "Contact ID (C)",
    string ContactIdInterceptNumber = "555",
    string ContactIdPhoneLineMode = "Match approved system design",
    int ContactIdInputGain = 10,
    string ContactIdFourXxLetter = "U",
    int ContactIdTtlHours = 3,
    int ContactIdTtlMinutes = 0,
    bool ContactIdBlindDialEnabled = false)
{
    public static IReadOnlyList<ProgrammingTemplate> Defaults { get; } =
    [
        new(
            "7788F — Standard fire",
            AesModel.Aes7788F,
            "0000",
            24,
            0,
            "RM",
            10,
            true,
            "FFFBBBBB",
            "RRRXXXXX",
            true,
            false,
            false,
            false,
            AntennaCatalog.All[0].DisplayName),
        new(
            "7744F — Standard 4x4 fire",
            AesModel.Aes7744F,
            "0000",
            24,
            0,
            "RM",
            10,
            true,
            "FFFFBBBB",
            "RRRRXXXX",
            true,
            false,
            false,
            false,
            AntennaCatalog.All[0].DisplayName)
    ];
}

public sealed record AesMapCoverageResult(
    string Antenna,
    decimal GainDb,
    int? ExpectedNetCon,
    int NetConFivePeers,
    int NetConSixOrSevenPeers,
    double Latitude,
    double Longitude,
    string Source)
{
    public bool HasCoverage => ExpectedNetCon.HasValue;

    public string Summary => HasCoverage
        ? $"{Antenna}: expected NETCON {ExpectedNetCon}, nearby N5 {NetConFivePeers}, nearby N6/7 {NetConSixOrSevenPeers}"
        : $"{Antenna}: no mapped signal at this point";
}

public sealed record AesMapCoverageAnalysis(
    double Latitude,
    double Longitude,
    IReadOnlyList<AesMapCoverageResult> Results,
    AesMapCoverageResult? Recommended)
{
    public string RecommendationSummary => Recommended is null
        ? "No Emergency24 map layer predicts usable coverage at this point."
        : $"Lowest mapped option at NETCON 0–5: {Recommended.Antenna} (expected NETCON {Recommended.ExpectedNetCon}).";
}

public sealed record BuildingSiteData(
    string FormattedAddress,
    double Latitude,
    double Longitude,
    double? GroundElevationMeters,
    double? RoofElevationMeters,
    double? EstimatedBuildingHeightMeters,
    double? RoofAreaSquareMeters,
    double? RoofPitchDegrees,
    double? RoofAzimuthDegrees,
    string ImageryQuality,
    string Notes);

public sealed record RadioSiteRecommendation(
    string Antenna,
    string Location,
    string Rationale,
    string Limitations);

public sealed class VirtualRadio
{
    public string RadioId { get; set; } = "1001";
    public string Name { get; set; } = "Radio 1";
    public AesModel Model { get; set; } = AesModel.Aes7788F;
    public int NetCon { get; set; } = 3;
    public int LinkLayer { get; set; } = 1;
    public string Quality { get; set; } = "03";
    public bool Online { get; set; } = true;

    public string DisplayName => $"{RadioId} · {Name}";
}

public sealed record VirtualMeshSignal(
    DateTimeOffset Timestamp,
    string SourceId,
    string DestinationId,
    string SignalType,
    string Route,
    string Result,
    decimal ProbabilityPercent,
    string Detail);
