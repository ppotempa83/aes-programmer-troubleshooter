using Microsoft.Extensions.DependencyInjection;
using SuperiorAes.Android.Services;

namespace SuperiorAes.Android.Pages;

public partial class StartupPage : ContentPage
{
    private readonly IServiceProvider _services;
    private bool _transitionStarted;
    private string _startupDiagnostic = string.Empty;

    public StartupPage(IServiceProvider services)
    {
        InitializeComponent();
        _services = services;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (_transitionStarted)
        {
            return;
        }

        _transitionStarted = true;
        var delay = Task.Delay(TimeSpan.FromMilliseconds(3500));
        try
        {
            await delay;
            var appShell = _services.GetRequiredService<AppShell>();
            if (Window is not null)
            {
                Window.Page = appShell;
            }
        }
        catch (Exception exception)
        {
            await delay;
            _startupDiagnostic = BuildDiagnostic(exception);
            StartupFailureLabel.Text =
                $"The app stayed open instead of terminating. Copy this diagnostic and send it to the developer.\n\n{exception.GetType().Name}: {exception.Message}";
            StartupFailurePanel.IsVisible = true;

            try
            {
                var path = Path.Combine(FileSystem.Current.AppDataDirectory, "startup-error.txt");
                await File.WriteAllTextAsync(path, _startupDiagnostic);
            }
            catch (Exception storageException) when (storageException is not OutOfMemoryException)
            {
                // The on-screen copy action remains available if local storage fails.
            }
        }
    }

    private async void OnCopyDiagnosticClicked(object? sender, EventArgs args)
    {
        if (_startupDiagnostic.Length > 0)
        {
            await Clipboard.Default.SetTextAsync(_startupDiagnostic);
        }
    }

    private static string BuildDiagnostic(Exception exception)
    {
        var builder = new System.Text.StringBuilder();
        builder.AppendLine("AES Programmer & Troubleshooter Android startup diagnostic");
        builder.AppendLine($"UTC: {DateTimeOffset.UtcNow:O}");
        builder.AppendLine($"OS: {DeviceInfo.Current.Platform} {DeviceInfo.Current.VersionString}");
        builder.AppendLine($"Device: {DeviceInfo.Current.Manufacturer} {DeviceInfo.Current.Model}");
        builder.AppendLine($"App: {AppInfo.Current.VersionString} ({AppInfo.Current.BuildString})");
        builder.AppendLine();
        builder.Append(exception);
        return builder.ToString();
    }
}
