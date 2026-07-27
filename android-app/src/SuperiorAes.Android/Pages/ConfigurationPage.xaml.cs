using System.Text;
using SuperiorAes.Android.Services;

namespace SuperiorAes.Android.Pages;

public partial class ConfigurationPage : ContentPage
{
    private readonly ICompanionSession _session;
    private readonly CredentialMigrationService _credentials;
    private bool _automaticMigrationChecked;

    public ConfigurationPage(
        ICompanionSession session,
        CredentialMigrationService credentials)
    {
        InitializeComponent();
        _session = session;
        _credentials = credentials;
        TechnicianEntry.Text = _session.TechnicianName;
        AutomaticPathLabel.Text =
            $"Automatic app-private staging path:\n{_credentials.AppDataFilePath}";
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (_automaticMigrationChecked)
        {
            return;
        }

        _automaticMigrationChecked = true;
        var result = await _credentials.TryMigrateAppDataFileAsync();
        if (result.Error is not null)
        {
            ImportStatusLabel.Text =
                $"An app-private {CredentialMigrationService.LocalFileName} was found but could not be migrated: {result.Error}";
        }
        else if (result.FileFound)
        {
            ImportStatusLabel.Text =
                $"Automatically migrated {result.ImportedCount} populated credential variable(s) to Android Secure Storage. The app-private plaintext staging copy was removed.";
        }
    }

    private void OnSaveMetadataClicked(object? sender, EventArgs args)
    {
        _session.SetTechnicianName(TechnicianEntry.Text ?? string.Empty);
        ImportStatusLabel.Text = "Session label updated.";
    }

    private async void OnCopyTemplateClicked(object? sender, EventArgs args)
    {
        await Clipboard.Default.SetTextAsync(CredentialTemplateEditor.Text);
        _session.RecordActivity("Blank credential template copied");
    }

    private async void OnShareTemplateClicked(object? sender, EventArgs args)
    {
        var directory = Path.Combine(FileSystem.Current.AppDataDirectory, "Exports");
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, "credentials.template.txt");
        await File.WriteAllTextAsync(
            path,
            CredentialTemplateEditor.Text ?? string.Empty,
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        _session.RecordActivity("Blank credential template shared");
        await Share.Default.RequestAsync(
            new ShareFileRequest("Superior AES blank credential template", new ShareFile(path)));
    }

    private async void OnImportCredentialsClicked(object? sender, EventArgs args)
    {
        try
        {
            var selected = await FilePicker.Default.PickAsync(
                new PickOptions { PickerTitle = "Import Superior AES credentials.txt" });
            if (selected is null)
            {
                return;
            }

            await using var stream = await selected.OpenReadAsync();
            var imported = await _credentials.ImportSelectedFileAsync(stream);

            ImportStatusLabel.Text =
                $"Imported {imported} populated credential variable(s) into Android Secure Storage. Values are hidden.";
            _session.RecordActivity($"Credential file imported · {imported} value(s) migrated and redacted");
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            ImportStatusLabel.Text = $"Import failed: {exception.Message}";
        }
    }

}
