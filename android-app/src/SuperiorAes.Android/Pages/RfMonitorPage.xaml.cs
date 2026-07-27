using Microsoft.Maui.ApplicationModel;
using SuperiorAes.Android.Services;
using SuperiorAes.Core.Protocol;

namespace SuperiorAes.Android.Pages;

public partial class RfMonitorPage : ContentPage
{
    private readonly ICompanionSession _session;
    private bool _receiveEnabled;
    private bool _transmitEnabled;
    private bool _allEnabled;

    public RfMonitorPage(ICompanionSession session)
    {
        InitializeComponent();
        _session = session;
        _session.StateChanged += OnSessionChanged;
    }

    private void OnSessionChanged(object? sender, EventArgs args) =>
        MainThread.BeginInvokeOnMainThread(RefreshOutput);

    private async void OnReceiveClicked(object? sender, EventArgs args)
    {
        if (await SendAsync(AesCommand.ReceiveMonitor))
        {
            _receiveEnabled = !_receiveEnabled;
            if (!_receiveEnabled)
            {
                _allEnabled = false;
            }

            UpdateButtons();
        }
    }

    private async void OnTransmitClicked(object? sender, EventArgs args)
    {
        if (await SendAsync(AesCommand.TransmitMonitor))
        {
            _transmitEnabled = !_transmitEnabled;
            UpdateButtons();
        }
    }

    private async void OnAllClicked(object? sender, EventArgs args)
    {
        if (!_allEnabled && !_receiveEnabled)
        {
            if (!await SendAsync(AesCommand.ReceiveMonitor))
            {
                return;
            }

            _receiveEnabled = true;
        }

        if (await SendAsync(AesCommand.MonitorAll))
        {
            _allEnabled = !_allEnabled;
            UpdateButtons();
        }
    }

    private async void OnStopClicked(object? sender, EventArgs args)
    {
        if (_allEnabled)
        {
            await SendAsync(AesCommand.MonitorAll);
        }
        if (_transmitEnabled)
        {
            await SendAsync(AesCommand.TransmitMonitor);
        }
        if (_receiveEnabled)
        {
            await SendAsync(AesCommand.ReceiveMonitor);
        }

        _allEnabled = false;
        _transmitEnabled = false;
        _receiveEnabled = false;
        UpdateButtons();
    }

    private async void OnKeyClicked(object? sender, EventArgs args)
    {
        if (!RfLoadCheckBox.IsChecked || !AccountTestCheckBox.IsChecked)
        {
            await DisplayAlertAsync(
                "Safety confirmations required",
                "Confirm both the correct RF load/antenna and account-on-test conditions.",
                "OK");
            return;
        }

        if (await DisplayAlertAsync(
                "Real RF transmission warning",
                "Physical mode can key the transmitter. Confirm safe separation, approved load, and authorization.",
                "Key briefly",
                "Cancel"))
        {
            await SendAsync(AesCommand.KeyTransmitter);
        }
    }

    private async void OnAbortClicked(object? sender, EventArgs args)
    {
        try
        {
            await _session.SendEnterAsync();
        }
        catch (InvalidOperationException exception)
        {
            await DisplayAlertAsync("Unable to abort", exception.Message, "OK");
        }
    }

    private async Task<bool> SendAsync(AesCommand command)
    {
        try
        {
            if (!_session.IsConnected)
            {
                await _session.ConnectAsync();
            }

            await _session.SendCommandAsync(command);
            return true;
        }
        catch (Exception exception) when (exception is InvalidOperationException or IOException or TimeoutException)
        {
            await DisplayAlertAsync("Monitor command failed", exception.Message, "OK");
            return false;
        }
    }

    private void UpdateButtons()
    {
        ReceiveButton.Text = $"Receive: {OnOff(_receiveEnabled)}";
        TransmitButton.Text = $"Transmit: {OnOff(_transmitEnabled)}";
        AllButton.Text = $"All: {OnOff(_allEnabled)}";
    }

    private void RefreshOutput()
    {
        MonitorOutputLabel.Text = string.Join(
            Environment.NewLine,
            _session.Entries
                .Where(entry => entry.Channel is "RX" or "TX")
                .TakeLast(30)
                .Select(entry => entry.Formatted));
    }

    private static string OnOff(bool value) => value ? "ON" : "OFF";
}
