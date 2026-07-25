using System.Text;
using SuperiorAes.Core.Connections;

namespace SuperiorAes.Core.Protocol;

public sealed class AesProtocolClient : IAsyncDisposable
{
    private readonly IAesConnection _connection;
    private readonly SemaphoreSlim _conversationLock = new(1, 1);

    public AesProtocolClient(IAesConnection connection)
    {
        _connection = connection;
        _connection.DataReceived += ForwardDataReceived;
    }

    public event EventHandler<AesDataReceivedEventArgs>? DataReceived;

    public bool IsConnected => _connection.IsConnected;
    public string DisplayName => _connection.DisplayName;

    public Task ConnectAsync(CancellationToken cancellationToken = default) =>
        _connection.ConnectAsync(cancellationToken);

    public Task DisconnectAsync(CancellationToken cancellationToken = default) =>
        _connection.DisconnectAsync(cancellationToken);

    public Task SendCommandAsync(AesCommand command, CancellationToken cancellationToken = default) =>
        _connection.SendAsync(AesCommands.GetBytes(command), cancellationToken);

    public Task SendLineAsync(string value, CancellationToken cancellationToken = default)
    {
        var bytes = Encoding.ASCII.GetBytes(value + "\r");
        return _connection.SendAsync(bytes, cancellationToken);
    }

    public Task SendRawAsync(string value, CancellationToken cancellationToken = default) =>
        _connection.SendAsync(Encoding.ASCII.GetBytes(value), cancellationToken);

    public async Task RunConversationAsync(
        AesCommand command,
        IEnumerable<string> responses,
        TimeSpan? responseDelay = null,
        CancellationToken cancellationToken = default)
    {
        await _conversationLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await SendCommandAsync(command, cancellationToken).ConfigureAwait(false);
            var delay = responseDelay ?? TimeSpan.FromMilliseconds(650);
            foreach (var response in responses)
            {
                await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
                await SendLineAsync(response, cancellationToken).ConfigureAwait(false);
            }
        }
        finally
        {
            _conversationLock.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        _connection.DataReceived -= ForwardDataReceived;
        _conversationLock.Dispose();
        await _connection.DisposeAsync().ConfigureAwait(false);
    }

    private void ForwardDataReceived(object? sender, AesDataReceivedEventArgs args) =>
        DataReceived?.Invoke(this, args);
}
