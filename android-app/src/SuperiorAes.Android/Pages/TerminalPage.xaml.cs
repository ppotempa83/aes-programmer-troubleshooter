using Microsoft.Maui.ApplicationModel;
using SuperiorAes.Android.Services;

namespace SuperiorAes.Android.Pages;

public partial class TerminalPage : ContentPage
{
    private readonly ICompanionSession _session;

    public TerminalPage(ICompanionSession session)
    {
        InitializeComponent();
        _session = session;
        _session.StateChanged += OnSessionChanged;
        Refresh();
    }

    private void OnSessionChanged(object? sender, EventArgs args) =>
        MainThread.BeginInvokeOnMainThread(Refresh);

    private async void OnSendClicked(object? sender, EventArgs args)
    {
        var value = TerminalEntry.Text ?? string.Empty;
        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        try
        {
            if (!_session.IsConnected)
            {
                await _session.ConnectAsync();
            }

            TerminalEntry.Text = string.Empty;
            await _session.SendRawAsync(value);
        }
        catch (Exception exception) when (
            exception is InvalidOperationException or IOException or UnauthorizedAccessException)
        {
            await DisplayAlertAsync("Terminal send failed", exception.Message, "OK");
        }
    }

    private async void OnEnterClicked(object? sender, EventArgs args)
    {
        try
        {
            if (!_session.IsConnected)
            {
                await _session.ConnectAsync();
            }

            await _session.SendEnterAsync();
        }
        catch (Exception exception) when (
            exception is InvalidOperationException or IOException or UnauthorizedAccessException)
        {
            await DisplayAlertAsync("Terminal send failed", exception.Message, "OK");
        }
    }

    private void Refresh() => TerminalOutputLabel.Text = _session.Transcript;
}
