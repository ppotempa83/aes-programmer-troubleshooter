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
}

