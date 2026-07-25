namespace SuperiorAes.Core.Connections;

public sealed class AesDataReceivedEventArgs(string text) : EventArgs
{
    public string Text { get; } = text;
}

public interface IAesConnection : IAsyncDisposable
{
    event EventHandler<AesDataReceivedEventArgs>? DataReceived;
    event EventHandler? ConnectionStateChanged;

    string DisplayName { get; }
    bool IsConnected { get; }

    Task ConnectAsync(CancellationToken cancellationToken = default);
    Task DisconnectAsync(CancellationToken cancellationToken = default);
    Task SendAsync(ReadOnlyMemory<byte> data, CancellationToken cancellationToken = default);
}

