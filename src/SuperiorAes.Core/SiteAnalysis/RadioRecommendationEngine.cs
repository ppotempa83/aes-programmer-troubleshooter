using SuperiorAes.Core.Models;

namespace SuperiorAes.Core.SiteAnalysis;

public static class RadioRecommendationEngine
{
    public static RadioSiteRecommendation Recommend(
        AesMapCoverageAnalysis coverage,
        BuildingSiteData? building,
        string construction,
        string preferredLocation)
    {
        var mapped = coverage.Recommended;
        var selected = mapped?.Antenna ?? "6 dB high-gain fiberglass";
        var severeAttenuation = construction.Contains("metal", StringComparison.OrdinalIgnoreCase) ||
                                construction.Contains("concrete", StringComparison.OrdinalIgnoreCase) ||
                                construction.Contains("high-rise", StringComparison.OrdinalIgnoreCase);

        if (severeAttenuation && mapped is not null && mapped.GainDb < 5)
        {
            selected = "5 dB high-gain stainless";
        }

        var location = preferredLocation.Contains("automatic", StringComparison.OrdinalIgnoreCase)
            ? severeAttenuation
                ? "Exterior wall or roof, vertically mounted above nearby obstructions"
                : "Highest practical interior test point first; move outdoors only if controlled testing requires it"
            : preferredLocation;

        var heightText = building?.EstimatedBuildingHeightMeters is double height
            ? $" Imagery suggests an approximate building height of {height:0.#} m."
            : string.Empty;
        var mapText = mapped is null
            ? "The Emergency24 layers did not predict a NETCON 0–5 result at the supplied point."
            : $"Emergency24 predicts NETCON {mapped.ExpectedNetCon} with {mapped.Antenna}.";
        var rationale =
            $"{mapText}{heightText} Construction selection: {construction}. Use the lowest-gain option that passes controlled NETCON, route-diversity, Q, and acknowledgement tests.";

        return new RadioSiteRecommendation(
            selected,
            location,
            rationale,
            "Planning estimate only. Verify frequency compatibility, vertical polarization, listed mounting, surge protection for exterior antennas, shortest practical 50-ohm coax, AHJ requirements, and end-to-end central-station receipt.");
    }
}
