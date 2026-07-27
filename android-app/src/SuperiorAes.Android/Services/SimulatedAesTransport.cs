using System.Text;
using SuperiorAes.Core.Protocol;

namespace SuperiorAes.Android.Services;

public sealed class SimulatedAesTransport : IAesTransport
{
    public event EventHandler<AesTransportDataReceivedEventArgs>? DataReceived;
    public event EventHandler? ConnectionStateChanged;

    public string DisplayName => "SIM-ANDROID-USB-RJ12 · 4800 8-N-1";
    public bool IsConnected { get; private set; }

    public Task ConnectAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!IsConnected)
        {
            IsConnected = true;
            ConnectionStateChanged?.Invoke(this, EventArgs.Empty);
            DataReceived?.Invoke(
                this,
                new AesTransportDataReceivedEventArgs(
                    "SUB [SIM-ANDROID] 7788F ID#:7740 RT1:AA11 LEVEL:086 STAT:000 NETCON:3"));
        }

        return Task.CompletedTask;
    }

    public Task DisconnectAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (IsConnected)
        {
            IsConnected = false;
            ConnectionStateChanged?.Invoke(this, EventArgs.Empty);
        }

        return Task.CompletedTask;
    }

    public Task SendAsync(ReadOnlyMemory<byte> data, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!IsConnected)
        {
            throw new InvalidOperationException("The simulated AES transport is not connected.");
        }

        var command = TryGetCommand(data.Span);
        var value = Encoding.ASCII.GetString(data.Span).TrimEnd('\r', '\n');
        var response = command switch
        {
            AesCommand.LocalStatus =>
                "SUB [SIM-ANDROID] 7788F ID#:7740 RT1:AA11 LEVEL:086 STAT:000 NETCON:3",
            AesCommand.RoutingTable =>
                "1.AA11,L:01,N:0,Q:03\r\n2.BB22,L:02,N:2,Q:02\r\n3.CC33,L:02,N:3,Q:01",
            AesCommand.ZoneStatus => "Zx00,Z1-8:0000-0000",
            AesCommand.ReceiveMonitor => "SIM RX MONITOR TOGGLED\r\nRX AA11 → 7740 · L1 · N0 · Q03",
            AesCommand.TransmitMonitor => "SIM TX MONITOR TOGGLED\r\nTX 7740 → AA11 · ACK",
            AesCommand.MonitorAll => "SIM MONITOR ALL TOGGLED\r\nRX AA11 → 7740\r\nTX 7740 → AA11 · ACK",
            AesCommand.KeyTransmitter => "SIMULATION · transmitter key request recorded; no RF transmission occurred",
            AesCommand.ProgramIdCipher => "SIM ID PROGRAMMING PROMPT",
            AesCommand.ProgramTimers => "SIM TIMER PROGRAMMING PROMPT",
            AesCommand.ProgramZones => "SIM ZONE PROGRAMMING PROMPT",
            AesCommand.ProgramModes => "SIM MODE PROGRAMMING PROMPT",
            AesCommand.ResetRam => "SIM RESET RAM PROMPT; no state was changed",
            AesCommand.Function1 => "SIM F1 PROMPT",
            AesCommand.TimeToLive => "SIM TTL PROMPT",
            AesCommand.Function3 => "SIM F3 PROMPT",
            AesCommand.SendText => "SIM TEXT PROMPT",
            _ when data.Span.SequenceEqual(new byte[] { 0x1B }) => "SIM ESC",
            _ when string.IsNullOrWhiteSpace(value) => "SIM ENTER",
            _ => $"SIM RESPONSE ACCEPTED · {value}"
        };
        DataReceived?.Invoke(this, new AesTransportDataReceivedEventArgs(response));
        return Task.CompletedTask;
    }

    public async ValueTask DisposeAsync()
    {
        await DisconnectAsync().ConfigureAwait(false);
    }

    private static AesCommand? TryGetCommand(ReadOnlySpan<byte> data)
    {
        foreach (var command in Enum.GetValues<AesCommand>())
        {
            if (data.SequenceEqual(AesCommands.GetBytes(command).Span))
            {
                return command;
            }
        }

        return null;
    }
}
