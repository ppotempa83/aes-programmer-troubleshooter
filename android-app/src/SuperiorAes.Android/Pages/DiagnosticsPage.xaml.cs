using Microsoft.Maui.ApplicationModel;
using SuperiorAes.Android.Services;
using SuperiorAes.Core.Protocol;

namespace SuperiorAes.Android.Pages;

public partial class DiagnosticsPage : ContentPage
{
    private readonly ICompanionSession _session;

    public DiagnosticsPage(ICompanionSession session)
    {
        InitializeComponent();
        _session = session;
        _session.StateChanged += OnSessionChanged;
        Refresh();
    }

    private void OnSessionChanged(object? sender, EventArgs args) =>
        MainThread.BeginInvokeOnMainThread(Refresh);

    private async void OnStatusClicked(object? sender, EventArgs args) =>
        await SendReadAsync(AesCommand.LocalStatus, "Local-status command sent; waiting for subscriber reply.");

    private async void OnRoutesClicked(object? sender, EventArgs args) =>
        await SendReadAsync(AesCommand.RoutingTable, "Routing-table command sent; waiting for subscriber reply.");

    private async void OnZonesClicked(object? sender, EventArgs args) =>
        await SendReadAsync(AesCommand.ZoneStatus, "Zone-status command sent; waiting for subscriber reply.");

    private async Task SendReadAsync(AesCommand command, string state)
    {
        try
        {
            if (!_session.IsConnected)
            {
                await _session.ConnectAsync();
            }

            await _session.SendCommandAsync(command);
            CommandStateLabel.Text = state;
        }
        catch (Exception exception) when (exception is InvalidOperationException or IOException or TimeoutException)
        {
            CommandStateLabel.Text = $"Read failed: {exception.Message}";
        }
    }

    private void Refresh()
    {
        var status = _session.LastStatus;
        StatusOutputLabel.Text = status is null
            ? "No complete status reply captured."
            : $"{status.Model} · firmware {Value(status.Firmware)}\n" +
              $"ID#:{status.SubscriberId} · RT1:{status.RouteOne} · LEVEL:{status.Level:000}\n" +
              $"STAT:{status.StatCode} · NETCON:{status.NetCon} · {(status.IsEnrolled ? "ENROLLED" : "NOT VERIFIED")}";
        RoutesOutputLabel.Text = _session.Routes.Count == 0
            ? "No route reply captured."
            : string.Join(
                Environment.NewLine,
                _session.Routes.Select(route =>
                    $"{route.Preference}.{route.Id},L:{route.LinkLayer:00},N:{route.PeerNetCon},Q:{route.Quality} · {route.QualityLabel}"));
        ZonesOutputLabel.Text = _session.Zones.Count == 0
            ? "No zone reply captured."
            : string.Join(
                " · ",
                _session.Zones.Select(zone => $"Z{zone.Zone}:{zone.State} {zone.Label}"));
    }

    private static string Value(string value) => string.IsNullOrWhiteSpace(value) ? "not reported" : value;
}
