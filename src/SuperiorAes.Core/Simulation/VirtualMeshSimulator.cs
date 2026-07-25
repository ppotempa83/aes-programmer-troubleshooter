using SuperiorAes.Core.Diagnostics;
using SuperiorAes.Core.Models;

namespace SuperiorAes.Core.Simulation;

public sealed class VirtualMeshSimulator
{
    private readonly Random _random;

    public VirtualMeshSimulator(int? randomSeed = null)
    {
        _random = randomSeed.HasValue ? new Random(randomSeed.Value) : Random.Shared;
    }

    public IReadOnlyList<VirtualMeshSignal> Send(
        IReadOnlyList<VirtualRadio> radios,
        string sourceId,
        string destinationId,
        string signalType)
    {
        ArgumentNullException.ThrowIfNull(radios);
        if (radios.Count is < 1 or > 4)
        {
            throw new ArgumentOutOfRangeException(nameof(radios), "A virtual mesh must contain one through four radios.");
        }

        var source = radios.FirstOrDefault(
            radio => string.Equals(radio.RadioId, sourceId, StringComparison.OrdinalIgnoreCase))
            ?? throw new ArgumentException("Source radio was not found.", nameof(sourceId));

        var targets = string.Equals(destinationId, "BROADCAST", StringComparison.OrdinalIgnoreCase)
            ? radios.Where(radio => !ReferenceEquals(radio, source)).ToArray()
            : radios.Where(radio =>
                string.Equals(radio.RadioId, destinationId, StringComparison.OrdinalIgnoreCase)).ToArray();

        if (targets.Length == 0)
        {
            throw new ArgumentException("No destination radio was found.", nameof(destinationId));
        }

        return targets.Select(target => SimulatePath(radios, source, target, signalType)).ToArray();
    }

    public static decimal CalculateSuccessPercent(VirtualRadio source, VirtualRadio destination)
    {
        if (!source.Online || !destination.Online)
        {
            return 0;
        }

        var quality = (DiagnosticEngine.QualityScore(source.Quality) +
                       DiagnosticEngine.QualityScore(destination.Quality)) / 12m;
        var netCon = (NetConFactor(source.NetCon) + NetConFactor(destination.NetCon)) / 2m;
        var layerPenalty = Math.Max(0, Math.Max(source.LinkLayer, destination.LinkLayer) - 1) * 0.07m;
        var probability = (0.18m + quality * 0.52m + netCon * 0.30m - layerPenalty) * 100m;
        return Math.Round(Math.Clamp(probability, 2m, 99m), 1);
    }

    private VirtualMeshSignal SimulatePath(
        IReadOnlyList<VirtualRadio> radios,
        VirtualRadio source,
        VirtualRadio destination,
        string signalType)
    {
        var probability = CalculateSuccessPercent(source, destination);
        var possibleRelay = radios
            .Where(radio => radio.Online &&
                            !ReferenceEquals(radio, source) &&
                            !ReferenceEquals(radio, destination) &&
                            radio.NetCon <= 5)
            .OrderByDescending(radio => DiagnosticEngine.QualityScore(radio.Quality))
            .ThenBy(radio => radio.LinkLayer)
            .FirstOrDefault();

        var directSucceeded = _random.NextDouble() * 100d <= (double)probability;
        var route = $"{source.RadioId} → {destination.RadioId}";
        var succeeded = directSucceeded;
        var detail = directSucceeded
            ? $"TX {signalType}; RX and ACK received from {destination.RadioId}."
            : $"Direct TX {signalType} was not acknowledged by {destination.RadioId}.";

        if (!succeeded && possibleRelay is not null)
        {
            var relayProbability = Math.Clamp(
                (CalculateSuccessPercent(source, possibleRelay) +
                 CalculateSuccessPercent(possibleRelay, destination)) / 2m - 8m,
                1m,
                96m);
            succeeded = _random.NextDouble() * 100d <= (double)relayProbability;
            probability = relayProbability;
            route = $"{source.RadioId} → {possibleRelay.RadioId} → {destination.RadioId}";
            detail = succeeded
                ? $"Direct path missed; {possibleRelay.RadioId} repeated the packet and {destination.RadioId} ACKed."
                : $"Direct and repeated paths were not acknowledged by {destination.RadioId}.";
        }

        return new VirtualMeshSignal(
            DateTimeOffset.Now,
            source.RadioId,
            destination.RadioId,
            signalType,
            route,
            succeeded ? "RECEIVED / ACK" : "DROPPED",
            probability,
            detail);
    }

    private static decimal NetConFactor(int netCon) =>
        netCon switch
        {
            <= 3 => 1m,
            4 => 0.92m,
            5 => 0.82m,
            6 => 0.48m,
            _ => 0.12m
        };
}
