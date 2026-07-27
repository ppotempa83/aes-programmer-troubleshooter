namespace SuperiorAes.Android.Services;

public enum AesTransportMode
{
    Simulation,
    FtdiUsbBench
}

/// <summary>
/// Explicit transport selector. Simulation remains selected at startup. The
/// hardware mode cannot be selected unless the caller records acceptance of the
/// cable and J1 safety warning.
/// </summary>
public interface IAesTransportSelector : IAesTransport
{
    AesTransportMode SelectedMode { get; }
    IReadOnlyList<AesTransportMode> AvailableModes { get; }

    Task SelectModeAsync(
        AesTransportMode mode,
        bool hardwareSafetyWarningAccepted,
        CancellationToken cancellationToken = default);
}
