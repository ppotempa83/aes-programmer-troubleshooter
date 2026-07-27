namespace SuperiorAes.Core.Protocol;

public enum AesCommand
{
    Function1,
    TimeToLive,
    Function3,
    RoutingTable,
    SendText,
    ZoneStatus,
    ReceiveMonitor,
    TransmitMonitor,
    MonitorAll,
    LocalStatus,
    KeyTransmitter,
    ProgramIdCipher,
    ProgramTimers,
    ProgramZones,
    ProgramModes,
    ResetRam
}

public static class AesCommands
{
    public const int BaudRate = 4800;

    public static ReadOnlyMemory<byte> GetBytes(AesCommand command) =>
        command switch
        {
            AesCommand.Function1 => new byte[] { 0x11 },
            AesCommand.TimeToLive => new byte[] { 0x12 },
            AesCommand.Function3 => new byte[] { 0x13 },
            AesCommand.RoutingTable => new byte[] { 0x14 },
            AesCommand.SendText => new byte[] { 0x15 },
            AesCommand.ZoneStatus => new byte[] { 0x1A },
            AesCommand.ReceiveMonitor => "a"u8.ToArray(),
            AesCommand.TransmitMonitor => "b"u8.ToArray(),
            AesCommand.MonitorAll => "c"u8.ToArray(),
            AesCommand.LocalStatus => "d"u8.ToArray(),
            AesCommand.KeyTransmitter => "e"u8.ToArray(),
            AesCommand.ProgramIdCipher => "f"u8.ToArray(),
            AesCommand.ProgramTimers => "g"u8.ToArray(),
            AesCommand.ProgramZones => "h"u8.ToArray(),
            AesCommand.ProgramModes => "i"u8.ToArray(),
            AesCommand.ResetRam => "j"u8.ToArray(),
            _ => throw new ArgumentOutOfRangeException(nameof(command), command, null)
        };

    public static string DisplayName(AesCommand command) =>
        command switch
        {
            AesCommand.Function1 => "F1 / Ctrl+Q",
            AesCommand.TimeToLive => "Time-to-Live / Ctrl+R",
            AesCommand.Function3 => "F3 / Ctrl+S",
            AesCommand.RoutingTable => "Routing table / Ctrl+T",
            AesCommand.SendText => "Send text / Ctrl+U",
            AesCommand.ZoneStatus => "Zone status / Ctrl+Z",
            AesCommand.ReceiveMonitor => "Receive monitor / a",
            AesCommand.TransmitMonitor => "Transmit monitor / b",
            AesCommand.MonitorAll => "Monitor all / c",
            AesCommand.LocalStatus => "Local status / d",
            AesCommand.KeyTransmitter => "Key transmitter / e",
            AesCommand.ProgramIdCipher => "Program ID and cipher / f",
            AesCommand.ProgramTimers => "Program timers / g",
            AesCommand.ProgramZones => "Program zones / h",
            AesCommand.ProgramModes => "Program modes / i",
            AesCommand.ResetRam => "Reset RAM / j",
            _ => command.ToString()
        };

    public static IReadOnlyList<AesCommandGuide> Guides { get; } =
    [
        new(AesCommand.Function1, "Advanced F1", "Opens the firmware-specific F1 function.", "Follow the prompt shown by the subscriber.", "Press the button, then enter the requested value."),
        new(AesCommand.TimeToLive, "Program Time-to-Live", "Opens the interactive TTL programming menu.", "Enter the TTL value requested by the firmware, then press ENTER.", "Example: 24"),
        new(AesCommand.Function3, "Advanced F3", "Opens the firmware-specific F3 function.", "Follow the prompt shown by the subscriber.", "Press the button, then enter the requested value."),
        new(AesCommand.RoutingTable, "Read full routing table", "Reads all stored network routes in preference order.", "No input is required.", "Example response: 1.AA11,L:01,N:0,Q:03"),
        new(AesCommand.SendText, "Send text message", "Starts the interactive central-station text-message function.", "Enter only the destination/message format requested by the connected firmware.", "Example: follow the live subscriber prompt"),
        new(AesCommand.ZoneStatus, "Read zone status", "Reads the current state of all subscriber inputs.", "No input is required.", "Example response: Z1-8:0000-0000"),
        new(AesCommand.ReceiveMonitor, "Toggle receive monitor", "Turns live receive-packet monitoring on or off.", "No input is required.", "Press again to turn the monitor off."),
        new(AesCommand.TransmitMonitor, "Toggle transmit monitor", "Turns live transmit-packet monitoring on or off.", "No input is required.", "Press again to turn the monitor off."),
        new(AesCommand.MonitorAll, "Toggle monitor all", "Shows receive and transmit mesh traffic. Receive monitor is enabled first when needed.", "No input is required.", "Press again to turn the monitor off."),
        new(AesCommand.LocalStatus, "Read local status", "Reads model, ID, route, level, STAT, and NETCON.", "No input is required.", "Example response: ID#:1A2B STAT:000 NETCON:3"),
        new(AesCommand.KeyTransmitter, "Key transmitter", "Keys RF briefly for an authorized power test.", "Requires an antenna/dummy load, account on test, and the separate safety confirmation.", "Press ENTER to abort an active test."),
        new(AesCommand.ProgramIdCipher, "Program ID and cipher", "Programs the four-digit subscriber ID and optionally replaces the system cipher.", "Use the guided ID/cipher panel. Both values are four uppercase hexadecimal characters.", "Example ID: 1A2B; leave cipher blank to preserve it."),
        new(AesCommand.ProgramTimers, "Program timers", "Programs check-in, AC reporting, and normal reporting delays.", "Use the guided timer panel: HH, MM, RM or 0–60, then 0–330 seconds.", "Example: 24, 00, RM, 10"),
        new(AesCommand.ProgramZones, "Program zones/restorals", "Programs eight zone types and eight restoral flags.", "Use the guided zone panel. Zones use F/S/B; restorals use R/X.", "Example: FFFBBBBB and RRRXXXXX"),
        new(AesCommand.ProgramModes, "Program operating modes", "Programs repeating and model-specific trouble-report suppression.", "Use the guided operating-mode checkboxes.", "Listed fire example: repeating Y; all suppressions N"),
        new(AesCommand.ResetRam, "Reset RAM", "Destructively returns timers, zones, restorals, and modes to defaults.", "Use the red Reset RAM panel and type RESET RAM.", "Subscriber ID and cipher remain; this is not board RESET.")
    ];
}

public sealed record AesCommandGuide(
    AesCommand Command,
    string Title,
    string Explanation,
    string EntryFormat,
    string Example);

