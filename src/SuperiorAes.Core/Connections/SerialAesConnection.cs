using System.IO.Ports;
using System.Text;
using SuperiorAes.Core.Protocol;

namespace SuperiorAes.Core.Connections;

public sealed class SerialAesConnection(string portName) : IAesConnection
{
    private readonly SerialPort _port = new(
        portName,
        AesCommands.BaudRate,
        Parity.None,
        8,
        StopBits.One)
    {
        Handshake = Handshake.None,
        Encoding = Encoding.ASCII,
        DtrEnable = true,
        RtsEnable = false,
        ReadTimeout = 1000,
        WriteTimeout = 1000,
        NewLine = "\r"
    };

    public event EventHandler<AesDataReceivedEventArgs>? DataReceived;
    public event EventHandler? ConnectionStateChanged;

    public string DisplayName => $"{portName} · 4800 8-N-1";
    public bool IsConnected => _port.IsOpen;

    public static IReadOnlyList<string> GetAvailablePorts() =>
        SerialPort.GetPortNames()
            .OrderBy(name => ParsePortNumber(name))
            .ThenBy(name => name, StringComparer.OrdinalIgnoreCase)
            .ToArray();

    public Task ConnectAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (_port.IsOpen)
        {
            return Task.CompletedTask;
        }

        _port.DataReceived += OnDataReceived;
        _port.Open();
        ConnectionStateChanged?.Invoke(this, EventArgs.Empty);
        return Task.CompletedTask;
    }

    public Task DisconnectAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!_port.IsOpen)
        {
            return Task.CompletedTask;
        }

        _port.DataReceived -= OnDataReceived;
        _port.Close();
        ConnectionStateChanged?.Invoke(this, EventArgs.Empty);
        return Task.CompletedTask;
    }

    public Task SendAsync(ReadOnlyMemory<byte> data, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!_port.IsOpen)
        {
            throw new InvalidOperationException("The AES serial connection is not open.");
        }

        var buffer = data.ToArray();
        _port.Write(buffer, 0, buffer.Length);
        return Task.CompletedTask;
    }

    public async ValueTask DisposeAsync()
    {
        await DisconnectAsync().ConfigureAwait(false);
        _port.Dispose();
    }

    private void OnDataReceived(object sender, SerialDataReceivedEventArgs args)
    {
        try
        {
            var text = _port.ReadExisting();
            if (!string.IsNullOrEmpty(text))
            {
                DataReceived?.Invoke(this, new AesDataReceivedEventArgs(text));
            }
        }
        catch (InvalidOperationException)
        {
            // The port can close between the event and the read during disconnect.
        }
        catch (IOException)
        {
            // The UI receives the state change on disconnect; a transient device removal is
            // surfaced by the next send/connect attempt without crashing the receive thread.
        }
    }

    private static int ParsePortNumber(string value) =>
        int.TryParse(value.AsSpan(3), out var number) ? number : int.MaxValue;
}
