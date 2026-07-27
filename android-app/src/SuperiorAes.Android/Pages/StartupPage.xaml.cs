using SuperiorAes.Android.Services;

namespace SuperiorAes.Android.Pages;

public partial class StartupPage : ContentPage
{
    private readonly AppShell _appShell;
    private readonly CredentialMigrationService _credentials;
    private bool _transitionStarted;

    public StartupPage(
        AppShell appShell,
        CredentialMigrationService credentials)
    {
        InitializeComponent();
        _appShell = appShell;
        _credentials = credentials;
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
        await _credentials.TryMigrateAppDataFileAsync();
        await delay;
        if (Window is not null)
        {
            Window.Page = _appShell;
        }
    }
}
