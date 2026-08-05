using SuperiorAes.Android.Services;

namespace SuperiorAes.Android.Pages;

public partial class ConfigurationPage : ContentPage
{
    private readonly ICompanionSession _session;

    public ConfigurationPage(ICompanionSession session)
    {
        InitializeComponent();
        _session = session;
        TechnicianEntry.Text = _session.TechnicianName;
    }

    private void OnSaveMetadataClicked(object? sender, EventArgs args)
    {
        _session.SetTechnicianName(TechnicianEntry.Text ?? string.Empty);
        ImportStatusLabel.Text = "Session label updated.";
    }

    private async void OnOpenGeoapifyClicked(object? sender, EventArgs args)
    {
        await Launcher.Default.OpenAsync("https://myprojects.geoapify.com/");
        ImportStatusLabel.Text = "Opened Geoapify MyProjects in the browser.";
        _session.RecordActivity("Official Geoapify MyProjects page opened");
    }
}
