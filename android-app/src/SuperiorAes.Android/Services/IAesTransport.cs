namespace SuperiorAes.Android.Services;

public sealed class AesTransportDataReceivedEventArgs(string text) : EventArgs
{
    public string Text { get; } = text;
}

public interface IAesTransport : IAsyncDisposable
{
    event EventHandler<AesTransportDataReceivedEventArgs>? DataReceived;
    event EventHandler? ConnectionStateChanged;

    string DisplayName { get; }
    bool IsConnected { get; }

    Task ConnectAsync(CancellationToken cancellationToken = default);
    Task DisconnectAsync(CancellationToken cancellationToken = default);
    Task SendAsync(ReadOnlyMemory<byte> data, CancellationToken cancellationToken = default);
}

