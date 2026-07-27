using System.Text;
using global::Android.App;
using global::Android.Content;
using global::Android.Hardware.Usb;
using SuperiorAes.Android.Services;

namespace SuperiorAes.Android.Platforms.Android;

/// <summary>
/// Minimal FTDI SIO driver implemented over Android USB Host APIs. It is
/// intentionally limited to known FTDI VID/PID pairs and the AES programming
/// settings used by the Windows application.
/// </summary>
public sealed class FtdiUsbTransport : IFtdiUsbTransport, IFtdiD2xxTransport
{
    private const int FtdiVendorId = 0x0403;
    private const int FtdiFt232ProductId = 0x6001;
    private const int MinimumSupportedBcdDevice = 0x0400;
    private const int MaximumSupportedBcdDevice = 0x06ff;

    private const int UsbControlTimeoutMilliseconds = 5000;
    private const int UsbWriteTimeoutMilliseconds = 2000;
    private const int UsbReadTimeoutMilliseconds = 250;
    private const int ReadPacketCount = 16;
    private const int FtdiStatusHeaderLength = 2;
    private const int FtdiLatencyMilliseconds = 16;

    private const int ResetRequest = 0;
    private const int ModemControlRequest = 1;
    private const int SetFlowControlRequest = 2;
    private const int SetBaudRateRequest = 3;
    private const int SetDataRequest = 4;
    private const int SetLatencyTimerRequest = 9;

    private const int ResetAll = 0;
    private const int ResetPurgeReceive = 1;
    private const int ResetPurgeTransmit = 2;
    private const int ModemControlDtrEnable = 0x0101;
    private const int ModemControlRtsDisable = 0x0200;
    private const int DataBitsEightParityNoneStopBitsOne = 0x0008;

    private const string UsbPermissionAction =
        "com.superiorfirellc.superioraesprogrammer.USB_PERMISSION";

    private static readonly HashSet<int> SupportedProductIds =
    [
        FtdiFt232ProductId
    ];

    private readonly Context _context;
    private readonly UsbManager _usbManager;
    private readonly UsbBroadcastReceiver _receiver;
    private readonly PendingIntent _permissionIntent;
    private readonly SemaphoreSlim _lifecycleGate = new(1, 1);
    private readonly SemaphoreSlim _writeGate = new(1, 1);
    private readonly object _stateGate = new();
    private readonly object _permissionGate = new();

    private UsbDevice? _device;
    private int? _bcdDevice;
    private UsbDeviceConnection? _connection;
    private UsbInterface? _claimedInterface;
    private UsbEndpoint? _writeEndpoint;
    private CancellationTokenSource? _readCancellation;
    private Task? _readTask;
    private TaskCompletionSource<bool>? _permissionCompletion;
    private int? _permissionDeviceId;
    private bool _receiverRegistered;
    private bool _disposed;
    private bool _isConnected;

    public FtdiUsbTransport()
    {
        _context = global::Android.App.Application.Context;
        _usbManager =
            (UsbManager?)_context.GetSystemService(Context.UsbService) ??
            throw new PlatformNotSupportedException("Android USB Host service is unavailable.");
        _receiver = new UsbBroadcastReceiver(this);

        var permissionRequest = new Intent(UsbPermissionAction);
        permissionRequest.SetPackage(_context.PackageName);
        _permissionIntent = PendingIntent.GetBroadcast(
                _context,
                0,
                permissionRequest,
                PendingIntentFlags.UpdateCurrent | PendingIntentFlags.Immutable) ??
            throw new InvalidOperationException("Unable to create the Android USB permission request.");
        permissionRequest.Dispose();

        RegisterUsbReceiver();
    }

    public event EventHandler<AesTransportDataReceivedEventArgs>? DataReceived;
    public event EventHandler? ConnectionStateChanged;

    public bool HasSupportedDevice => FindSupportedDevice() is not null;

    public bool HasUsbPermission
    {
        get
        {
            var device = FindSupportedDevice();
            return device is not null && _usbManager.HasPermission(device);
        }
    }

    public string? AttachedDeviceIdentity
    {
        get
        {
            var device = FindSupportedDevice();
            if (device is null)
            {
                return null;
            }

            lock (_stateGate)
            {
                var bcdDevice = _device?.DeviceId == device.DeviceId
                    ? _bcdDevice
                    : null;
                return FormatIdentity(device, bcdDevice);
            }
        }
    }

    public string DisplayName
    {
        get
        {
            lock (_stateGate)
            {
                return _device is null
                    ? "FTDI USB bench transport · 4800 8-N-1"
                    : $"{FormatIdentity(_device, _bcdDevice)} · 4800 8-N-1";
            }
        }
    }

    public bool IsConnected
    {
        get
        {
            lock (_stateGate)
            {
                return _isConnected;
            }
        }
    }

    public async Task RequestUsbPermissionAsync(
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        var device = FindSupportedDevice() ??
            throw new InvalidOperationException(
                "No supported FTDI USB serial adapter is attached. " +
                SelectableAesTransport.HardwareSafetyWarning);

        if (_usbManager.HasPermission(device))
        {
            return;
        }

        TaskCompletionSource<bool> completion;
        lock (_permissionGate)
        {
            if (_permissionCompletion is not null &&
                _permissionDeviceId == device.DeviceId)
            {
                completion = _permissionCompletion;
            }
            else
            {
                completion = new TaskCompletionSource<bool>(
                    TaskCreationOptions.RunContinuationsAsynchronously);
                _permissionCompletion = completion;
                _permissionDeviceId = device.DeviceId;
                try
                {
                    _usbManager.RequestPermission(device, _permissionIntent);
                }
                catch
                {
                    _permissionCompletion = null;
                    _permissionDeviceId = null;
                    throw;
                }
            }
        }

        bool granted;
        try
        {
            granted = await completion.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            lock (_permissionGate)
            {
                if (ReferenceEquals(_permissionCompletion, completion) &&
                    completion.Task.IsCompleted)
                {
                    _permissionCompletion = null;
                    _permissionDeviceId = null;
                }
            }
        }

        if (!granted || !_usbManager.HasPermission(device))
        {
            throw new UnauthorizedAccessException(
                "Android USB permission was denied for the FTDI adapter. " +
                SelectableAesTransport.HardwareSafetyWarning);
        }
    }

    public async Task ConnectAsync(CancellationToken cancellationToken = default)
    {
        await _lifecycleGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            ThrowIfDisposed();
            if (IsConnected)
            {
                return;
            }

            var device = FindSupportedDevice() ??
                throw new InvalidOperationException(
                    "No supported FTDI USB serial adapter is attached. " +
                    SelectableAesTransport.HardwareSafetyWarning);

            await RequestUsbPermissionAsync(cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();

            var openedConnection = await Task.Run(
                    () => OpenAndConfigureDevice(device),
                    CancellationToken.None)
                .ConfigureAwait(false);
            var ownershipTransferred = false;
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                var readCancellation = new CancellationTokenSource();
                lock (_stateGate)
                {
                    _device = device;
                    _bcdDevice = openedConnection.BcdDevice;
                    _connection = openedConnection.Connection;
                    _claimedInterface = openedConnection.ClaimedInterface;
                    _writeEndpoint = openedConnection.WriteEndpoint;
                    _readCancellation = readCancellation;
                    _isConnected = true;
                    _readTask = Task.Run(
                        () => ReadLoopAsync(
                            device,
                            openedConnection.Connection,
                            openedConnection.ReadEndpoint,
                            readCancellation.Token),
                        CancellationToken.None);
                }

                ownershipTransferred = true;
                ConnectionStateChanged?.Invoke(this, EventArgs.Empty);
            }
            finally
            {
                if (!ownershipTransferred)
                {
                    await Task.Run(
                            () => CloseOpenedConnection(openedConnection),
                            CancellationToken.None)
                        .ConfigureAwait(false);
                }
            }
        }
        finally
        {
            _lifecycleGate.Release();
        }
    }

    public async Task DisconnectAsync(CancellationToken cancellationToken = default)
    {
        DisconnectedResources? resources;
        await _lifecycleGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await _writeGate.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                resources = BeginDisconnect();
            }
            finally
            {
                _writeGate.Release();
            }
        }
        finally
        {
            _lifecycleGate.Release();
        }

        NotifyDisconnected(resources);
        await CompleteDisconnectAsync(resources, waitForReader: true).ConfigureAwait(false);
    }

    public async Task SendAsync(
        ReadOnlyMemory<byte> data,
        CancellationToken cancellationToken = default)
    {
        if (data.IsEmpty)
        {
            return;
        }

        ThrowIfDisposed();
        await _writeGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        UsbDeviceConnection? failedConnection = null;
        Exception? writeException = null;
        try
        {
            UsbDeviceConnection connection;
            UsbEndpoint endpoint;
            lock (_stateGate)
            {
                if (!_isConnected || _connection is null || _writeEndpoint is null)
                {
                    throw new InvalidOperationException(
                        "The FTDI USB bench transport is not connected.");
                }

                connection = _connection;
                endpoint = _writeEndpoint;
            }

            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                var succeeded = await Task.Run(
                        () => WriteAllBytes(connection, endpoint, data.ToArray()),
                        CancellationToken.None)
                    .ConfigureAwait(false);
                if (!succeeded)
                {
                    failedConnection = connection;
                }
            }
            catch (Exception exception)
            {
                failedConnection = connection;
                writeException = exception;
            }
        }
        finally
        {
            _writeGate.Release();
        }

        if (failedConnection is not null)
        {
            await MarkConnectionLostAsync(failedConnection).ConfigureAwait(false);
            throw new IOException(
                "The FTDI USB write failed or the adapter was disconnected. " +
                SelectableAesTransport.HardwareSafetyWarning,
                writeException);
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        DisconnectedResources? resources;
        await _lifecycleGate.WaitAsync().ConfigureAwait(false);
        try
        {
            await _writeGate.WaitAsync().ConfigureAwait(false);
            try
            {
                if (_disposed)
                {
                    return;
                }

                _disposed = true;
                resources = BeginDisconnect();

                if (_receiverRegistered)
                {
                    try
                    {
                        _context.UnregisterReceiver(_receiver);
                    }
                    catch (ArgumentException)
                    {
                        // The process may already have removed the dynamic receiver.
                    }

                    _receiverRegistered = false;
                }

                lock (_permissionGate)
                {
                    _permissionCompletion?.TrySetCanceled();
                    _permissionCompletion = null;
                    _permissionDeviceId = null;
                }
            }
            finally
            {
                _writeGate.Release();
            }
        }
        finally
        {
            _lifecycleGate.Release();
        }

        NotifyDisconnected(resources);
        await CompleteDisconnectAsync(resources, waitForReader: true).ConfigureAwait(false);
        _permissionIntent.Dispose();
        _receiver.Dispose();
    }

    private void RegisterUsbReceiver()
    {
        var filter = new IntentFilter(UsbPermissionAction);
        filter.AddAction(UsbManager.ActionUsbDeviceDetached);

        if (OperatingSystem.IsAndroidVersionAtLeast(33))
        {
            _context.RegisterReceiver(_receiver, filter, ReceiverFlags.NotExported);
        }
        else
        {
#pragma warning disable CS0618
#pragma warning disable CA1422
            _context.RegisterReceiver(_receiver, filter);
#pragma warning restore CA1422
#pragma warning restore CS0618
        }

        filter.Dispose();
        _receiverRegistered = true;
    }

    private void HandleBroadcast(Intent intent)
    {
        if (intent.Action == UsbPermissionAction)
        {
            TaskCompletionSource<bool>? completion;
            var permissionDevice = GetUsbDeviceExtra(intent);
            lock (_permissionGate)
            {
                if (_permissionDeviceId is null ||
                    permissionDevice is null ||
                    permissionDevice.DeviceId != _permissionDeviceId.Value)
                {
                    return;
                }

                completion = _permissionCompletion;
            }

            completion?.TrySetResult(
                intent.GetBooleanExtra(UsbManager.ExtraPermissionGranted, false));
            return;
        }

        if (intent.Action == UsbManager.ActionUsbDeviceDetached)
        {
            var detachedDevice = GetUsbDeviceExtra(intent);
            if (detachedDevice is null)
            {
                return;
            }

            TaskCompletionSource<bool>? pendingPermission = null;
            lock (_permissionGate)
            {
                if (_permissionDeviceId == detachedDevice.DeviceId)
                {
                    pendingPermission = _permissionCompletion;
                }
            }

            pendingPermission?.TrySetException(
                new IOException(
                    "The FTDI USB adapter was detached while Android permission was pending."));

            UsbDeviceConnection? currentConnection;
            int? currentDeviceId;
            lock (_stateGate)
            {
                currentConnection = _connection;
                currentDeviceId = _device?.DeviceId;
            }

            if (currentConnection is not null &&
                currentDeviceId == detachedDevice.DeviceId)
            {
                _ = MarkConnectionLostAsync(currentConnection);
            }
        }
    }

    private async Task ReadLoopAsync(
        UsbDevice device,
        UsbDeviceConnection connection,
        UsbEndpoint endpoint,
        CancellationToken cancellationToken)
    {
        var packetSize = Math.Max(endpoint.MaxPacketSize, FtdiStatusHeaderLength + 1);
        var buffer = new byte[packetSize * ReadPacketCount];

        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var bytesRead = connection.BulkTransfer(
                    endpoint,
                    buffer,
                    buffer.Length,
                    UsbReadTimeoutMilliseconds);
                if (bytesRead < 0)
                {
                    if (!IsDeviceAttached(device.DeviceId))
                    {
                        break;
                    }

                    continue;
                }

                if (bytesRead <= FtdiStatusHeaderLength)
                {
                    continue;
                }

                var payload = StripFtdiStatusHeaders(buffer, bytesRead, packetSize);
                if (payload.Length == 0)
                {
                    continue;
                }

                var text = Encoding.ASCII.GetString(payload);
                if (text.Length > 0)
                {
                    DataReceived?.Invoke(this, new AesTransportDataReceivedEventArgs(text));
                }
            }
        }
        catch (Exception) when (cancellationToken.IsCancellationRequested)
        {
            // Expected when the connection is closed to cancel a blocking USB read.
        }
        catch (Exception)
        {
            // USB removal can surface as several Java or IO exception types.
            // The finally block converts every one to a clean disconnected state.
        }
        finally
        {
            if (!cancellationToken.IsCancellationRequested)
            {
                await MarkConnectionLostAsync(connection).ConfigureAwait(false);
            }
        }
    }

    private async Task MarkConnectionLostAsync(UsbDeviceConnection connection)
    {
        DisconnectedResources? resources;
        await _lifecycleGate.WaitAsync().ConfigureAwait(false);
        try
        {
            await _writeGate.WaitAsync().ConfigureAwait(false);
            try
            {
                lock (_stateGate)
                {
                    if (!ReferenceEquals(_connection, connection))
                    {
                        return;
                    }
                }

                resources = BeginDisconnect();
            }
            finally
            {
                _writeGate.Release();
            }
        }
        finally
        {
            _lifecycleGate.Release();
        }

        NotifyDisconnected(resources);
        await CompleteDisconnectAsync(resources, waitForReader: false).ConfigureAwait(false);
    }

    private DisconnectedResources? BeginDisconnect()
    {
        UsbDeviceConnection? connection;
        UsbInterface? claimedInterface;
        CancellationTokenSource? readCancellation;
        Task? readTask;
        bool stateChanged;

        lock (_stateGate)
        {
            connection = _connection;
            claimedInterface = _claimedInterface;
            readCancellation = _readCancellation;
            readTask = _readTask;
            stateChanged = _isConnected;

            if (connection is null &&
                claimedInterface is null &&
                readCancellation is null &&
                readTask is null &&
                !stateChanged)
            {
                return null;
            }

            _isConnected = false;
            _device = null;
            _bcdDevice = null;
            _connection = null;
            _claimedInterface = null;
            _writeEndpoint = null;
            _readCancellation = null;
            _readTask = null;
        }

        readCancellation?.Cancel();
        if (connection is not null)
        {
            if (claimedInterface is not null)
            {
                TryReleaseInterface(connection, claimedInterface);
            }

            connection.Close();
        }

        return new DisconnectedResources(
            connection,
            readCancellation,
            readTask,
            stateChanged);
    }

    private static async Task CompleteDisconnectAsync(
        DisconnectedResources? resources,
        bool waitForReader)
    {
        if (resources is null)
        {
            return;
        }

        if (waitForReader && resources.ReadTask is not null)
        {
            try
            {
                await resources.ReadTask.ConfigureAwait(false);
            }
            catch (System.OperationCanceledException)
            {
                // The read loop is cancellation-driven.
            }
        }

        resources.ReadCancellation?.Dispose();
        resources.Connection?.Dispose();
    }

    private OpenedFtdiConnection OpenAndConfigureDevice(UsbDevice device)
    {
        var connection = _usbManager.OpenDevice(device) ??
            throw new IOException(
                "Android could not open the FTDI adapter after permission was granted.");
        UsbInterface? claimedInterface = null;

        try
        {
            var endpoints = FindBulkEndpoints(device);
            var bcdDevice = ReadAndValidateBcdDevice(connection);
            claimedInterface = endpoints.Interface;
            if (!connection.ClaimInterface(claimedInterface, true))
            {
                throw new IOException(
                    $"Android could not claim FTDI interface {claimedInterface.Id}.");
            }

            ConfigureFtdiPort(
                connection,
                endpoints.ReadEndpoint,
                endpoints.WriteEndpoint);
            return new OpenedFtdiConnection(
                connection,
                claimedInterface,
                endpoints.ReadEndpoint,
                endpoints.WriteEndpoint,
                bcdDevice);
        }
        catch
        {
            if (claimedInterface is not null)
            {
                TryReleaseInterface(connection, claimedInterface);
            }

            connection.Close();
            connection.Dispose();
            throw;
        }
    }

    private static void CloseOpenedConnection(OpenedFtdiConnection openedConnection)
    {
        TryReleaseInterface(
            openedConnection.Connection,
            openedConnection.ClaimedInterface);
        openedConnection.Connection.Close();
        openedConnection.Connection.Dispose();
    }

    private static bool WriteAllBytes(
        UsbDeviceConnection connection,
        UsbEndpoint endpoint,
        byte[] data)
    {
        var offset = 0;
        while (offset < data.Length)
        {
            var writeBuffer = offset == 0
                ? data
                : data.AsSpan(offset).ToArray();
            var written = connection.BulkTransfer(
                endpoint,
                writeBuffer,
                writeBuffer.Length,
                UsbWriteTimeoutMilliseconds);
            if (written <= 0)
            {
                return false;
            }

            offset += written;
        }

        return true;
    }

    private static int ReadAndValidateBcdDevice(UsbDeviceConnection connection)
    {
        var descriptors = connection.GetRawDescriptors();
        if (descriptors is null || descriptors.Length < 14)
        {
            throw new IOException(
                "The FTDI device descriptor is unavailable; hardware generation cannot be verified.");
        }

        var bcdDevice = descriptors[12] | (descriptors[13] << 8);
        if (bcdDevice < MinimumSupportedBcdDevice ||
            bcdDevice > MaximumSupportedBcdDevice)
        {
            throw new NotSupportedException(
                $"FTDI 0403:6001 bcdDevice 0x{bcdDevice:X4} is outside the supported " +
                "FT232B/R generation range (0x0400-0x06FF).");
        }

        return bcdDevice;
    }

    private void NotifyDisconnected(DisconnectedResources? resources)
    {
        if (resources?.StateChanged == true)
        {
            ConnectionStateChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    private void ConfigureFtdiPort(
        UsbDeviceConnection connection,
        UsbEndpoint readEndpoint,
        UsbEndpoint writeEndpoint)
    {
        if (readEndpoint.Direction != UsbAddressing.In ||
            writeEndpoint.Direction != UsbAddressing.Out)
        {
            throw new IOException("FTDI bulk endpoints have unexpected directions.");
        }

        const int legacyChannelIndex = 0;
        ControlTransfer(connection, ResetRequest, ResetAll, legacyChannelIndex, "reset");
        ControlTransfer(
            connection,
            ModemControlRequest,
            ModemControlDtrEnable | ModemControlRtsDisable,
            legacyChannelIndex,
            "set DTR/RTS");
        ControlTransfer(
            connection,
            SetFlowControlRequest,
            0,
            legacyChannelIndex,
            "disable flow control");

        var baud = EncodeBaudRate(FtdiD2xxTransportRequirements.BaudRate);
        ControlTransfer(
            connection,
            SetBaudRateRequest,
            baud.Value,
            baud.Index,
            "set 4800 baud");
        ControlTransfer(
            connection,
            SetDataRequest,
            DataBitsEightParityNoneStopBitsOne,
            legacyChannelIndex,
            "set 8-N-1");
        ControlTransfer(
            connection,
            SetLatencyTimerRequest,
            FtdiLatencyMilliseconds,
            legacyChannelIndex,
            "set latency timer");
        ControlTransfer(connection, ResetRequest, ResetPurgeReceive, legacyChannelIndex, "purge RX");
        ControlTransfer(connection, ResetRequest, ResetPurgeTransmit, legacyChannelIndex, "purge TX");
    }

    private static (int Value, int Index) EncodeBaudRate(int baudRate)
    {
        if (baudRate <= 0 || baudRate > 3_500_000)
        {
            throw new ArgumentOutOfRangeException(nameof(baudRate), baudRate, "Unsupported FTDI baud rate.");
        }

        int divisor;
        int subDivisor;
        if (baudRate >= 2_500_000)
        {
            divisor = 0;
            subDivisor = 0;
        }
        else if (baudRate >= 1_750_000)
        {
            divisor = 1;
            subDivisor = 0;
        }
        else
        {
            var eighthDivisor = (int)Math.Round(
                24_000_000d / baudRate,
                MidpointRounding.AwayFromZero);
            subDivisor = eighthDivisor & 0x07;
            divisor = eighthDivisor >> 3;
            if (divisor > 0x3fff)
            {
                throw new ArgumentOutOfRangeException(nameof(baudRate), baudRate, "FTDI baud rate is too low.");
            }
        }

        var value = divisor;
        var index = 0;
        switch (subDivisor)
        {
            case 0:
                break;
            case 4:
                value |= 0x4000;
                break;
            case 2:
                value |= 0x8000;
                break;
            case 1:
                value |= 0xc000;
                break;
            case 3:
                index |= 1;
                break;
            case 5:
                value |= 0x4000;
                index |= 1;
                break;
            case 6:
                value |= 0x8000;
                index |= 1;
                break;
            case 7:
                value |= 0xc000;
                index |= 1;
                break;
        }

        return (value, index);
    }

    private static void ControlTransfer(
        UsbDeviceConnection connection,
        int request,
        int value,
        int index,
        string operation)
    {
        var result = connection.ControlTransfer(
            (UsbAddressing)0x40,
            request,
            value,
            index,
            null,
            0,
            UsbControlTimeoutMilliseconds);
        if (result != 0)
        {
            throw new IOException($"FTDI {operation} failed with USB result {result}.");
        }
    }

    private static byte[] StripFtdiStatusHeaders(
        byte[] buffer,
        int bytesRead,
        int packetSize)
    {
        var payloadLength = 0;
        for (var packetOffset = 0; packetOffset < bytesRead; packetOffset += packetSize)
        {
            var packetLength = Math.Min(packetSize, bytesRead - packetOffset);
            if (packetLength > FtdiStatusHeaderLength)
            {
                payloadLength += packetLength - FtdiStatusHeaderLength;
            }
        }

        if (payloadLength == 0)
        {
            return [];
        }

        var payload = new byte[payloadLength];
        var destinationOffset = 0;
        for (var packetOffset = 0; packetOffset < bytesRead; packetOffset += packetSize)
        {
            var packetLength = Math.Min(packetSize, bytesRead - packetOffset);
            var packetPayloadLength = packetLength - FtdiStatusHeaderLength;
            if (packetPayloadLength <= 0)
            {
                continue;
            }

            Buffer.BlockCopy(
                buffer,
                packetOffset + FtdiStatusHeaderLength,
                payload,
                destinationOffset,
                packetPayloadLength);
            destinationOffset += packetPayloadLength;
        }

        return payload;
    }

    private (UsbInterface Interface, UsbEndpoint ReadEndpoint, UsbEndpoint WriteEndpoint)
        FindBulkEndpoints(UsbDevice device)
    {
        for (var interfaceIndex = 0; interfaceIndex < device.InterfaceCount; interfaceIndex++)
        {
            var usbInterface = device.GetInterface(interfaceIndex);
            UsbEndpoint? readEndpoint = null;
            UsbEndpoint? writeEndpoint = null;

            for (var endpointIndex = 0; endpointIndex < usbInterface.EndpointCount; endpointIndex++)
            {
                var endpoint = usbInterface.GetEndpoint(endpointIndex);
                if (endpoint is null)
                {
                    continue;
                }

                if (endpoint.Type != UsbAddressing.XferBulk)
                {
                    continue;
                }

                if (endpoint.Direction == UsbAddressing.In)
                {
                    readEndpoint = endpoint;
                }
                else if (endpoint.Direction == UsbAddressing.Out)
                {
                    writeEndpoint = endpoint;
                }
            }

            if (readEndpoint is not null && writeEndpoint is not null)
            {
                return (usbInterface, readEndpoint, writeEndpoint);
            }
        }

        throw new IOException("The FTDI device does not expose a usable bulk IN/OUT interface.");
    }

    private UsbDevice? FindSupportedDevice() =>
        _usbManager.DeviceList?.Values
            .Where(IsSupportedFtdiDevice)
            .OrderBy(device => device.DeviceId)
            .FirstOrDefault();

    private static bool IsSupportedFtdiDevice(UsbDevice device) =>
        device.VendorId == FtdiVendorId &&
        SupportedProductIds.Contains(device.ProductId);

    private bool IsDeviceAttached(int deviceId) =>
        _usbManager.DeviceList?.Values.Any(
            device => device.DeviceId == deviceId && IsSupportedFtdiDevice(device)) == true;

    private static string FormatIdentity(UsbDevice device, int? bcdDevice = null) =>
        bcdDevice is null
            ? $"FTDI USB {device.VendorId:X4}:{device.ProductId:X4}"
            : $"FTDI FT232B/R {device.VendorId:X4}:{device.ProductId:X4} " +
              $"bcdDevice 0x{bcdDevice.Value:X4}";

    private static UsbDevice? GetUsbDeviceExtra(Intent intent)
    {
#pragma warning disable CS0618
#pragma warning disable CA1422
        return intent.GetParcelableExtra(UsbManager.ExtraDevice) as UsbDevice;
#pragma warning restore CA1422
#pragma warning restore CS0618
    }

    private static void TryReleaseInterface(
        UsbDeviceConnection connection,
        UsbInterface usbInterface)
    {
        try
        {
            connection.ReleaseInterface(usbInterface);
        }
        catch (Exception)
        {
            // The Android USB stack can already have invalidated it after removal.
        }
    }

    private void ThrowIfDisposed() =>
        ObjectDisposedException.ThrowIf(_disposed, this);

    private sealed record DisconnectedResources(
        UsbDeviceConnection? Connection,
        CancellationTokenSource? ReadCancellation,
        Task? ReadTask,
        bool StateChanged);

    private sealed record OpenedFtdiConnection(
        UsbDeviceConnection Connection,
        UsbInterface ClaimedInterface,
        UsbEndpoint ReadEndpoint,
        UsbEndpoint WriteEndpoint,
        int BcdDevice);

    private sealed class UsbBroadcastReceiver(FtdiUsbTransport owner) : BroadcastReceiver
    {
        public override void OnReceive(Context? context, Intent? intent)
        {
            if (intent is not null)
            {
                owner.HandleBroadcast(intent);
            }
        }
    }
}
