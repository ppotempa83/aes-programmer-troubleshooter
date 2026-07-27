namespace SuperiorAes.Android.Services;

public sealed class SelectableAesTransport : IAesTransportSelector
{
    public const string HardwareSafetyWarning =
        "Hardware bench mode requires a genuine USB-to-RS-232 adapter. " +
        "AES J1 pin 6 carries +12 V and must not be connected. A bare USB-to-TTL cable is not safe.";

    private readonly SimulatedAesTransport _simulation;
    private readonly IFtdiUsbTransport _ftdi;
    private readonly SemaphoreSlim _lifecycleGate = new(1, 1);
    private readonly object _stateGate = new();
    private IAesTransport _activeTransport;
    private bool _disposed;

    public SelectableAesTransport(
        SimulatedAesTransport simulation,
        IFtdiUsbTransport ftdi)
    {
        _simulation = simulation;
        _ftdi = ftdi;
        _activeTransport = simulation;

        _simulation.DataReceived += OnTransportDataReceived;
        _simulation.ConnectionStateChanged += OnTransportConnectionStateChanged;
        _ftdi.DataReceived += OnTransportDataReceived;
        _ftdi.ConnectionStateChanged += OnTransportConnectionStateChanged;
    }

    public event EventHandler<AesTransportDataReceivedEventArgs>? DataReceived;
    public event EventHandler? ConnectionStateChanged;

    public AesTransportMode SelectedMode { get; private set; } = AesTransportMode.Simulation;

    public IReadOnlyList<AesTransportMode> AvailableModes { get; } =
        [AesTransportMode.Simulation, AesTransportMode.FtdiUsbBench];

    public string DisplayName
    {
        get
        {
            lock (_stateGate)
            {
                return _activeTransport.DisplayName;
            }
        }
    }

    public bool IsConnected
    {
        get
        {
            lock (_stateGate)
            {
                return _activeTransport.IsConnected;
            }
        }
    }

    public async Task SelectModeAsync(
        AesTransportMode mode,
        bool hardwareSafetyWarningAccepted,
        CancellationToken cancellationToken = default)
    {
        if (!AvailableModes.Contains(mode))
        {
            throw new ArgumentOutOfRangeException(nameof(mode), mode, "Unsupported AES transport mode.");
        }

        if (mode == AesTransportMode.FtdiUsbBench && !hardwareSafetyWarningAccepted)
        {
            throw new InvalidOperationException(HardwareSafetyWarning);
        }

        await _lifecycleGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            ThrowIfDisposed();
            if (SelectedMode == mode)
            {
                return;
            }

            IAesTransport previous;
            lock (_stateGate)
            {
                previous = _activeTransport;
            }

            if (previous.IsConnected)
            {
                await previous.DisconnectAsync(cancellationToken).ConfigureAwait(false);
            }

            lock (_stateGate)
            {
                _activeTransport = mode == AesTransportMode.FtdiUsbBench
                    ? _ftdi
                    : _simulation;
                SelectedMode = mode;
            }

            ConnectionStateChanged?.Invoke(this, EventArgs.Empty);
        }
        finally
        {
            _lifecycleGate.Release();
        }
    }

    public async Task ConnectAsync(CancellationToken cancellationToken = default)
    {
        await _lifecycleGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            ThrowIfDisposed();
            IAesTransport transport;
            lock (_stateGate)
            {
                transport = _activeTransport;
            }

            if (SelectedMode == AesTransportMode.FtdiUsbBench && !_ftdi.HasSupportedDevice)
            {
                throw new InvalidOperationException(
                    "No supported FTDI USB serial adapter is attached. " + HardwareSafetyWarning);
            }

            await transport.ConnectAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _lifecycleGate.Release();
        }
    }

    public async Task DisconnectAsync(CancellationToken cancellationToken = default)
    {
        await _lifecycleGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            IAesTransport transport;
            lock (_stateGate)
            {
                transport = _activeTransport;
            }

            await transport.DisconnectAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _lifecycleGate.Release();
        }
    }

    public Task SendAsync(
        ReadOnlyMemory<byte> data,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        IAesTransport transport;
        lock (_stateGate)
        {
            transport = _activeTransport;
        }

        return transport.SendAsync(data, cancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        await _lifecycleGate.WaitAsync().ConfigureAwait(false);
        try
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _simulation.DataReceived -= OnTransportDataReceived;
            _simulation.ConnectionStateChanged -= OnTransportConnectionStateChanged;
            _ftdi.DataReceived -= OnTransportDataReceived;
            _ftdi.ConnectionStateChanged -= OnTransportConnectionStateChanged;

            IAesTransport transport;
            lock (_stateGate)
            {
                transport = _activeTransport;
            }

            if (transport.IsConnected)
            {
                await transport.DisconnectAsync().ConfigureAwait(false);
            }
        }
        finally
        {
            _lifecycleGate.Release();
        }
    }

    private void OnTransportDataReceived(
        object? sender,
        AesTransportDataReceivedEventArgs args)
    {
        lock (_stateGate)
        {
            if (!ReferenceEquals(sender, _activeTransport))
            {
                return;
            }
        }

        DataReceived?.Invoke(this, args);
    }

    private void OnTransportConnectionStateChanged(object? sender, EventArgs args)
    {
        lock (_stateGate)
        {
            if (!ReferenceEquals(sender, _activeTransport))
            {
                return;
            }
        }

        ConnectionStateChanged?.Invoke(this, EventArgs.Empty);
    }

    private void ThrowIfDisposed() =>
        ObjectDisposedException.ThrowIf(_disposed, this);
}
