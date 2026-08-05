using Microsoft.Maui.ApplicationModel;
using SuperiorAes.Android.Services;

namespace SuperiorAes.Android.Pages;

public partial class DashboardPage : ContentPage
{
    private readonly ICompanionSession _session;
    private bool _changingTransport;

    public DashboardPage(ICompanionSession session)
    {
        InitializeComponent();
        _session = session;
        TransportPicker.ItemsSource = new[]
        {
            "Simulation (safe default)",
            "FTDI USB-to-RS-232 · hardware bench"
        };
        TransportPicker.SelectedIndex = _session.TransportMode == AesTransportMode.Simulation ? 0 : 1;
        ModelPicker.ItemsSource = new[] { "7744F", "7788F" };
        ModelPicker.SelectedItem = _session.SelectedModel;
        _session.StateChanged += OnSessionChanged;
        Refresh();
    }

    private void OnSessionChanged(object? sender, EventArgs args) =>
        MainThread.BeginInvokeOnMainThread(Refresh);

    private void OnModelChanged(object? sender, EventArgs args)
    {
        if (ModelPicker.SelectedItem is string model && model != _session.SelectedModel)
        {
            _session.SelectModel(model);
        }
    }

    private async void OnTransportChanged(object? sender, EventArgs args)
    {
        if (_changingTransport || TransportPicker.SelectedIndex < 0)
        {
            return;
        }

        var requested = TransportPicker.SelectedIndex == 0
            ? AesTransportMode.Simulation
            : AesTransportMode.FtdiUsbBench;
        if (requested == _session.TransportMode)
        {
            return;
        }

        var accepted = requested == AesTransportMode.Simulation ||
            await DisplayAlertAsync(
                "Hardware bench mode",
                "This mode can send real programming and RF commands through a genuine FTDI USB-to-RS-232 adapter. Verify a known-good 7744F/7788F, 4800 8-N-1, flow control OFF, and a correct RS-232 cable with AES J1 pin 6 (+12 V) completely isolated. This Android path is not yet field-validated.",
                "I accept the bench warning",
                "Keep simulation");
        if (!accepted)
        {
            _changingTransport = true;
            TransportPicker.SelectedIndex = 0;
            _changingTransport = false;
            return;
        }

        try
        {
            await _session.SelectTransportModeAsync(requested, accepted);
        }
        catch (Exception exception) when (exception is InvalidOperationException or IOException)
        {
            await DisplayAlertAsync("Transport selection failed", exception.Message, "OK");
            _changingTransport = true;
            TransportPicker.SelectedIndex = _session.TransportMode == AesTransportMode.Simulation ? 0 : 1;
            _changingTransport = false;
        }

        Refresh();
    }

    private async void OnConnectionClicked(object? sender, EventArgs args)
    {
        try
        {
            if (_session.IsConnected)
            {
                await _session.DisconnectAsync();
            }
            else
            {
                await _session.ConnectAsync();
            }
        }
        catch (Exception exception) when (
            exception is InvalidOperationException or IOException or UnauthorizedAccessException or TimeoutException)
        {
            await DisplayAlertAsync("Connection failed", exception.Message, "OK");
        }
    }

    private void Refresh()
    {
        ConnectionStatusLabel.Text = _session.IsConnected
            ? $"Connected · {_session.ConnectionName}"
            : "Disconnected";
        ConnectionStatusLabel.TextColor = _session.IsConnected
            ? Color.FromArgb("#16825D")
            : Color.FromArgb("#68788B");
        ConnectionButton.Text = _session.IsConnected ? "Disconnect" : "Connect";
        ConnectionButton.IsEnabled = !_session.IsBusy;
        TransportPicker.IsEnabled = !_session.IsBusy;
        ModelPicker.IsEnabled = !_session.IsConnected && !_session.IsBusy;
        var simulation = _session.TransportMode == AesTransportMode.Simulation;
        ModeBannerLabel.Text = simulation
            ? "SIMULATION · SAFE DEFAULT · 0 REAL TRANSMISSIONS"
            : "HARDWARE BENCH MODE · REAL COMMANDS POSSIBLE · NOT FIELD-VALIDATED";
        ModeBannerLabel.TextColor = simulation
            ? Color.FromArgb("#C5202F")
            : Color.FromArgb("#9D1725");
        SessionMetadataLabel.Text =
            $"Session {_session.SessionId:D}\n" +
            $"Started {_session.SessionStarted.LocalDateTime:[MM-dd-yyyy / hh:mm (tt)]}\n" +
            $"Subscriber {(_session.SubscriberId.Length == 0 ? "not read" : _session.SubscriberId)} · {_session.SelectedModel}";
        if (_session.IsBusy)
        {
            ConnectionStatusLabel.Text = _session.BusyMessage;
        }
        RecentActivityLabel.Text = string.Join(
            Environment.NewLine,
            _session.Entries.TakeLast(8).Select(entry => entry.Formatted));
    }
}
