using System.Text;
using SuperiorAes.Core.Models;
using SuperiorAes.Core.Protocol;

namespace SuperiorAes.Core.Connections;

public sealed class SimulatedAesConnection(AesModel model = AesModel.Aes7788F) : IAesConnection
{
    private bool _isConnected;
    private bool _receiveMonitor;
    private bool _transmitMonitor;
    private bool _monitorAll;
    private string? _conversation;
    private int _responseIndex;
    private readonly StringBuilder _lineBuffer = new();

    public event EventHandler<AesDataReceivedEventArgs>? DataReceived;
    public event EventHandler? ConnectionStateChanged;

    public string DisplayName => $"Simulation · {(model == AesModel.Aes7744F ? "7744F" : "7788F")}";
    public bool IsConnected => _isConnected;

    public async Task ConnectAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _isConnected = true;
        ConnectionStateChanged?.Invoke(this, EventArgs.Empty);
        await EmitAsync(
            $"\r\nSUPERIOR AES SIMULATION\r\nSUB [7.8] {(model == AesModel.Aes7744F ? "7744F" : "7788F")}\r\nID#:1A2B (C) AES\r\n",
            cancellationToken).ConfigureAwait(false);
    }

    public Task DisconnectAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _isConnected = false;
        ConnectionStateChanged?.Invoke(this, EventArgs.Empty);
        return Task.CompletedTask;
    }

    public async Task SendAsync(ReadOnlyMemory<byte> data, CancellationToken cancellationToken = default)
    {
        if (!_isConnected)
        {
            throw new InvalidOperationException("The AES simulator is not connected.");
        }

        var values = data.ToArray();
        foreach (var value in values)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (_conversation is not null)
            {
                if (value is 0x0D or 0x0A)
                {
                    var response = _lineBuffer.ToString();
                    _lineBuffer.Clear();
                    await HandleConversationResponseAsync(response, cancellationToken).ConfigureAwait(false);
                }
                else if (value is 0x08 or 0x7F)
                {
                    if (_lineBuffer.Length > 0)
                    {
                        _lineBuffer.Length--;
                    }
                }
                else
                {
                    _lineBuffer.Append((char)value);
                    await EmitAsync(((char)value).ToString(), cancellationToken, 5).ConfigureAwait(false);
                }
                continue;
            }

            await HandleCommandAsync(value, cancellationToken).ConfigureAwait(false);
        }
    }

    public async ValueTask DisposeAsync()
    {
        await DisconnectAsync().ConfigureAwait(false);
    }

    private async Task HandleCommandAsync(byte command, CancellationToken cancellationToken)
    {
        switch (command)
        {
            case (byte)'a':
                _receiveMonitor = !_receiveMonitor;
                await EmitAsync($"\r\nRX MONITOR {OnOff(_receiveMonitor)}\r\n", cancellationToken).ConfigureAwait(false);
                break;
            case (byte)'b':
                _transmitMonitor = !_transmitMonitor;
                await EmitAsync($"\r\nTX MONITOR {OnOff(_transmitMonitor)}\r\n", cancellationToken).ConfigureAwait(false);
                break;
            case (byte)'c':
                if (!_receiveMonitor && !_monitorAll)
                {
                    await EmitAsync("\r\nRX MONITOR REQUIRED\r\n", cancellationToken).ConfigureAwait(false);
                    break;
                }
                _monitorAll = !_monitorAll;
                await EmitAsync($"\r\nMONITOR ALL {OnOff(_monitorAll)}\r\n", cancellationToken).ConfigureAwait(false);
                if (_monitorAll)
                {
                    await EmitAsync("RX 1A2B P_RR PKT:31\r\nRX 4C21 D_CHKIN Q:03\r\nTX 1A2B P_ACK PKT:31\r\n", cancellationToken, 150).ConfigureAwait(false);
                }
                break;
            case (byte)'d':
                await EmitAsync(
                    $"\r\nSUB [7.8] {(model == AesModel.Aes7744F ? "7744F" : "7788F")}\r\nID#:1A2B (C) 2026 AES\r\nRT1:AA11 LEVEL:002\r\nSTAT:000 NETCON:3\r\n",
                    cancellationToken).ConfigureAwait(false);
                break;
            case (byte)'e':
                await EmitAsync("\r\nKEYING TX..\r\n", cancellationToken).ConfigureAwait(false);
                await EmitAsync("TIMEOUT\r\n", cancellationToken, 850).ConfigureAwait(false);
                break;
            case (byte)'f':
                StartConversation("id", "\r\nSETUP UNIT-OLD:NEW\r\nENTER ID#-1A2B:....\r\n");
                await EmitPendingAsync(cancellationToken).ConfigureAwait(false);
                break;
            case (byte)'g':
                StartConversation("timers", "\r\nCHKIN TIME-HRS 24:..\r\n");
                await EmitPendingAsync(cancellationToken).ConfigureAwait(false);
                break;
            case (byte)'h':
                StartConversation("zones", "\r\nFIRE/TROUBLE PKT-Y:.\r\n");
                await EmitPendingAsync(cancellationToken).ConfigureAwait(false);
                break;
            case (byte)'i':
                StartConversation("modes", "\r\nENABLE RPTNG-Y:.\r\n");
                await EmitPendingAsync(cancellationToken).ConfigureAwait(false);
                break;
            case (byte)'j':
                StartConversation("reset", "\r\nRESET RAM? Y/N\r\n");
                await EmitPendingAsync(cancellationToken).ConfigureAwait(false);
                break;
            case 0x12:
                StartConversation("ttl", "\r\nCHKIN TTL-HRS 24:..\r\n");
                await EmitPendingAsync(cancellationToken).ConfigureAwait(false);
                break;
            case 0x14:
                await EmitAsync(
                    "\r\n4.7D44,L:03,N:5,Q:83\r\n3.3C10,L:02,N:4,Q:02\r\n2.BB12,L:01,N:1,Q:03\r\n1.AA11,L:01,N:0,Q:03\r\n",
                    cancellationToken).ConfigureAwait(false);
                break;
            case 0x15:
                StartConversation("text", "\r\nENTER MSG:\r\n");
                await EmitPendingAsync(cancellationToken).ConfigureAwait(false);
                break;
            case 0x1A:
                await EmitAsync("\r\nZx00,Z1-8:0000-0000\r\n", cancellationToken).ConfigureAwait(false);
                break;
            case 0x0D:
                await EmitAsync("\r\nOK\r\n", cancellationToken).ConfigureAwait(false);
                break;
            default:
                await EmitAsync($"\r\n[{DescribeByte(command)}]\r\n", cancellationToken).ConfigureAwait(false);
                break;
        }
    }

    private async Task HandleConversationResponseAsync(string response, CancellationToken cancellationToken)
    {
        await EmitAsync("\r\n", cancellationToken, 10).ConfigureAwait(false);
        var conversation = _conversation;
        _responseIndex++;

        var nextPrompt = conversation switch
        {
            "id" when _responseIndex == 1 => "CPHR CODE-XXXX:....\r\n",
            "timers" when _responseIndex == 1 => "CHKIN TIME-MIN 00:..\r\n",
            "timers" when _responseIndex == 2 => "AC RPT TIME-RM:..\r\n",
            "timers" when _responseIndex == 3 => "NTR RPT DLY-10:...\r\n",
            "zones" when _responseIndex == 1 => "SET ZONE OLD FFFBBBBB\r\nNEW ........\r\n",
            "zones" when _responseIndex == 2 => "SET RESTORAL OLD RRRXXXXX\r\nNEW ........\r\n",
            "modes" when _responseIndex == 1 => "SPRSS ACFAIL-N:.\r\n",
            "modes" when _responseIndex == 2 && model == AesModel.Aes7788F => "SPRSS CHRGFLT-N:.\r\n",
            "modes" when _responseIndex == 3 && model == AesModel.Aes7788F => "SPRSS GNDFLT-N:.\r\n",
            "ttl" when _responseIndex < 14 => _responseIndex % 2 == 1
                ? "TTL-MIN 00:..\r\n"
                : "NEXT TTL-HRS 24:..\r\n",
            _ => null
        };

        var completeAt = conversation switch
        {
            "id" => 2,
            "timers" => 4,
            "zones" => 3,
            "modes" => model == AesModel.Aes7788F ? 4 : 2,
            "reset" => 1,
            "ttl" => 14,
            "text" => 1,
            _ => 1
        };

        if (_responseIndex >= completeAt)
        {
            var result = conversation == "reset" && !response.Equals("Y", StringComparison.OrdinalIgnoreCase)
                ? "CANCELLED"
                : "OK";
            _conversation = null;
            _responseIndex = 0;
            await EmitAsync($"{result}\r\n", cancellationToken).ConfigureAwait(false);
        }
        else if (nextPrompt is not null)
        {
            await EmitAsync(nextPrompt, cancellationToken).ConfigureAwait(false);
        }
    }

    private void StartConversation(string name, string firstPrompt)
    {
        _conversation = name;
        _responseIndex = 0;
        _lineBuffer.Clear();
        _pending = firstPrompt;
    }

    private string _pending = string.Empty;

    private async Task EmitPendingAsync(CancellationToken cancellationToken)
    {
        var pending = _pending;
        _pending = string.Empty;
        await EmitAsync(pending, cancellationToken).ConfigureAwait(false);
    }

    private async Task EmitAsync(string text, CancellationToken cancellationToken, int delayMs = 60)
    {
        await Task.Delay(delayMs, cancellationToken).ConfigureAwait(false);
        DataReceived?.Invoke(this, new AesDataReceivedEventArgs(text));
    }

    private static string OnOff(bool value) => value ? "ON" : "OFF";

    private static string DescribeByte(byte value) =>
        value is >= 0x20 and <= 0x7E ? ((char)value).ToString() : $"0x{value:X2}";
}
