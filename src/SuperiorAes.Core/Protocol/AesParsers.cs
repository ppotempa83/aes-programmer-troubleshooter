using System.Globalization;
using System.Text.RegularExpressions;
using SuperiorAes.Core.Models;

namespace SuperiorAes.Core.Protocol;

public static partial class AesParsers
{
    public static AesLocalStatus? ParseLocalStatus(string text)
    {
        var match = CompleteStatusRegex().Matches(text).Cast<Match>().LastOrDefault();
        if (match is null)
        {
            return null;
        }

        return new AesLocalStatus(
            match.Groups["model"].Value,
            match.Groups["firmware"].Value.Trim(),
            match.Groups["id"].Value.ToUpperInvariant(),
            match.Groups["route"].Value.ToUpperInvariant(),
            int.Parse(match.Groups["level"].Value, CultureInfo.InvariantCulture),
            match.Groups["stat"].Value.ToUpperInvariant(),
            int.Parse(match.Groups["netcon"].Value, CultureInfo.InvariantCulture));
    }

    public static IReadOnlyList<RouteEntry> ParseRoutes(string text)
    {
        var routes = new Dictionary<int, RouteEntry>();
        foreach (Match match in RouteRegex().Matches(text))
        {
            var preference = int.Parse(match.Groups["preference"].Value, CultureInfo.InvariantCulture);
            routes[preference] = new RouteEntry(
                preference,
                match.Groups["id"].Value.ToUpperInvariant(),
                int.Parse(match.Groups["layer"].Value, CultureInfo.InvariantCulture),
                int.Parse(match.Groups["netcon"].Value, CultureInfo.InvariantCulture),
                match.Groups["quality"].Value.ToUpperInvariant());
        }

        return routes.Values.OrderBy(route => route.Preference).ToArray();
    }

    public static IReadOnlyList<ZoneState> ParseZones(string text)
    {
        var matches = ZoneRegex().Matches(text);
        if (matches.Count == 0)
        {
            return Array.Empty<ZoneState>();
        }

        var match = matches[^1];
        var states = match.Groups["first"].Value + match.Groups["second"].Value;
        return states.Select((state, index) => new ZoneState(index + 1, state)).ToArray();
    }

    public static IReadOnlyList<StatFault> DecodeStat(string statCode)
    {
        if (!int.TryParse(statCode, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var value) ||
            value == 0)
        {
            return Array.Empty<StatFault>();
        }

        var definitions = new[]
        {
            (Mask: 0x001, Fault: new StatFault("001", "Low battery", "Test the battery and connections; correct the power condition, then use the board RESET button.")),
            (Mask: 0x002, Fault: new StatFault("002", "RAM checksum failure", "Use the board RESET button. If the fault repeats, record programming before considering a RAM reset.")),
            (Mask: 0x004, Fault: new StatFault("004", "EEPROM failure", "Use the board RESET button. Persistent faults require AES-authorized service.")),
            (Mask: 0x008, Fault: new StatFault("008", "ADC failure", "Use the board RESET button. Persistent faults require AES-authorized service.")),
            (Mask: 0x080, Fault: new StatFault("080", "Radio loopback failure", "Verify transceiver cabling and power. Persistent faults require service.")),
            (Mask: 0x100, Fault: new StatFault("100", "AC power absent", "Verify transformer power, wiring, and the unswitched AC source.")),
            (Mask: 0x200, Fault: new StatFault("200", "Battery charger failure", "Verify AC and battery wiring; test the charger and battery.")),
            (Mask: 0x400, Fault: new StatFault("400", "Ground fault", "Inspect zone wiring for an unintended connection to earth ground."))
        };

        return definitions
            .Where(definition => (value & definition.Mask) == definition.Mask)
            .Select(definition => definition.Fault)
            .ToArray();
    }

    [GeneratedRegex(@"\bSUB\s*(?:\[(?<firmware>[^\]]*)\])?.*?\b(?<model>7744F|7788F)\b.*?ID#:\s*(?<id>[0-9A-FX]{4}).*?RT1:\s*(?<route>[0-9A-FX]{4})\s+LEVEL:\s*(?<level>\d{1,3}).*?STAT:\s*(?<stat>[0-9A-F]{3})\s+NETCON:\s*(?<netcon>[0-7])", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex CompleteStatusRegex();

    [GeneratedRegex(@"(?m)^\s*(?<preference>[1-8])\.(?<id>[0-9A-F]{4}),L:(?<layer>\d{1,3}),N:(?<netcon>[0-7]),Q:(?<quality>[0-9A-F]{2})\s*$", RegexOptions.IgnoreCase)]
    private static partial Regex RouteRegex();

    [GeneratedRegex(@"Zx[0-9A-F]{1,2},Z1-8:(?<first>[01T]{4})-(?<second>[01T]{4})", RegexOptions.IgnoreCase)]
    private static partial Regex ZoneRegex();
}
