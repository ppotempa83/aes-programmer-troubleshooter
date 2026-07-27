namespace SuperiorAes.Android.Services;

/// <summary>
/// Android USB-host transport for supported FTDI USB serial devices.
/// The implementation uses Android's platform USB APIs and does not require a
/// vendor JAR, AAR, native library, or Windows VCP driver.
/// </summary>
public interface IFtdiUsbTransport : IAesTransport
{
    bool HasSupportedDevice { get; }
    bool HasUsbPermission { get; }
    string? AttachedDeviceIdentity { get; }

    Task RequestUsbPermissionAsync(CancellationToken cancellationToken = default);
}

// Kept as a source-compatible alias for the original Android scaffold contract.
public interface IFtdiD2xxTransport : IFtdiUsbTransport;

public static class FtdiD2xxTransportRequirements
{
    public const int BaudRate = 4800;
    public const int DataBits = 8;
    public const int StopBits = 1;
    public const bool ParityEnabled = false;
    public const bool FlowControlEnabled = false;
    public const bool DtrAsserted = true;
    public const bool RtsAsserted = false;

    public const string IntegrationStatus =
        "Android USB-host FTDI bench transport is available without a vendor binary. " +
        "Simulation remains the default until the physical-validation checklist passes.";
}
