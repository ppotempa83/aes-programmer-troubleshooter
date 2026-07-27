using System.Globalization;
using System.Text.Json;
using Microsoft.Maui.Controls.Shapes;
using SuperiorAes.Android.Models;
using SuperiorAes.Android.Services;
using SuperiorAes.Core.Models;
using SuperiorAes.Core.Protocol;
using SuperiorAes.Core.Templates;
using IOPath = System.IO.Path;

namespace SuperiorAes.Android.Pages;

public partial class ProgrammingPage : ContentPage
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    private readonly ICompanionSession _session;
    private readonly ProgrammingTemplateStore _templateStore;
    private readonly List<ProgrammingTemplate> _templates = [];
    private bool _templatesLoaded;

    public ProgrammingPage(ICompanionSession session)
    {
        InitializeComponent();
        _session = session;
        _templateStore = new ProgrammingTemplateStore(
            IOPath.Combine(FileSystem.Current.AppDataDirectory, "programming-templates.json"));
        TemplateModelPicker.ItemsSource = new[] { "7744F", "7788F" };
        TemplateAntennaPicker.ItemsSource = AntennaCatalog.All
            .Select(option => option.DisplayName)
            .ToArray();
        TemplateContactIdPicker.ItemsSource = new[]
        {
            "None",
            "7794 IntelliPro Fire (recommended)",
            "7067 IntelliTap II (historical / discontinued)",
            "7794A IntelliPro (IntelliNet 2.0 Fire only)"
        };
        TemplateContactFormatPicker.ItemsSource = new[]
        {
            "Contact ID (C)",
            "Pulse",
            "Modem IIe / IIIa2",
            "Other approved format"
        };
        TemplateContactPhoneLinePicker.ItemsSource = new[]
        {
            "Match approved system design",
            "Primary line",
            "Secondary line",
            "No line monitoring"
        };
        TemplatePicker.ItemDisplayBinding = new Binding(nameof(ProgrammingTemplate.Name));
        BuildCommandCards();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (_templatesLoaded)
        {
            return;
        }

        _templatesLoaded = true;
        await ReloadTemplatesAsync();
    }

    private void BuildCommandCards()
    {
        foreach (var definition in GuidedCommandCatalog.All)
        {
            var button = new Button
            {
                Text = definition.Command is null ? "Open Contact ID section" : "Explain, prompt, and run",
                CommandParameter = definition
            };
            button.Clicked += OnCommandClicked;

            var content = new VerticalStackLayout { Spacing = 7 };
            content.Children.Add(new Label
            {
                Text = definition.Title,
                FontAttributes = FontAttributes.Bold,
                FontSize = 17,
                TextColor = Color.FromArgb("#10253D")
            });
            content.Children.Add(new Label { Text = definition.Explanation, FontSize = 13 });
            content.Children.Add(new Label
            {
                Text = $"Entry format: {definition.EntryFormat}",
                FontSize = 12,
                TextColor = Color.FromArgb("#68788B")
            });
            content.Children.Add(new Label
            {
                Text = definition.Example,
                FontSize = 12,
                FontAttributes = FontAttributes.Italic,
                TextColor = definition.IsSafetyCritical
                    ? Color.FromArgb("#C5202F")
                    : Color.FromArgb("#68788B")
            });
            content.Children.Add(button);

            CommandList.Children.Add(new Border
            {
                Padding = 16,
                BackgroundColor = Colors.White,
                Stroke = Color.FromArgb("#DDE4EA"),
                StrokeThickness = 1,
                StrokeShape = new RoundRectangle { CornerRadius = new CornerRadius(10) },
                Content = content
            });
        }
    }

    private async void OnCommandClicked(object? sender, EventArgs args)
    {
        if (sender is not Button { CommandParameter: GuidedCommandDefinition definition })
        {
            return;
        }

        _session.RecordActivity($"Programming function opened · {definition.Title}");
        await DisplayAlertAsync(
            definition.Title,
            $"{definition.Explanation}\n\nENTRY FORMAT\n{definition.EntryFormat}\n\nEXAMPLE\n{definition.Example}",
            "Continue");

        if (definition.Command is null)
        {
            await Shell.Current.GoToAsync("//contact-id");
            return;
        }

        try
        {
            if (!await EnsureConnectedAsync())
            {
                return;
            }

            await RunCommandWorkflowAsync(definition.Command.Value);
        }
        catch (Exception exception) when (
            exception is InvalidOperationException or IOException or TimeoutException or ArgumentException)
        {
            _session.RecordActivity($"Programming function failed · {definition.Title}");
            await DisplayAlertAsync(
                "Command not completed",
                $"The command was not completed: {exception.Message}",
                "OK");
        }
    }

    private async Task RunCommandWorkflowAsync(AesCommand command)
    {
        switch (command)
        {
            case AesCommand.ProgramIdCipher:
                await ProgramIdentityAsync();
                return;
            case AesCommand.ProgramTimers:
                await ProgramTimersAsync();
                return;
            case AesCommand.ProgramZones:
                await ProgramZonesAsync();
                return;
            case AesCommand.ProgramModes:
                await ProgramModesAsync();
                return;
            case AesCommand.ResetRam:
                await ResetRamAsync();
                return;
            case AesCommand.KeyTransmitter:
                await KeyTransmitterAsync();
                return;
            case AesCommand.Function1:
            case AesCommand.TimeToLive:
            case AesCommand.Function3:
                await RunFirmwarePromptWorkflowAsync(command);
                return;
            case AesCommand.SendText:
                await SendTextMessageAsync();
                return;
        }

        if (!await DisplayAlertAsync(
                "Send command?",
                $"{AesCommands.DisplayName(command)} requires no typed value. Send it to {_session.ConnectionName} now?",
                "Send",
                "Cancel"))
        {
            return;
        }

        var firstEntryIndex = _session.Entries.Count;
        await _session.SendCommandAsync(command);
        await Task.Delay(TimeSpan.FromMilliseconds(450));
        await DisplayAlertAsync(
            $"{AesCommands.DisplayName(command)} sent",
            $"No input was sent after the command.\n\n{RecentSubscriberResponse(firstEntryIndex)}",
            "Done");
    }

    private async Task RunFirmwarePromptWorkflowAsync(AesCommand command)
    {
        var guide = AesCommands.Guides.First(value => value.Command == command);
        if (!await DisplayAlertAsync(
                $"{guide.Title} · live-prompt boundary",
                "The official 7744F/7788F material documents the F1/F2/F3 PC key equivalents but does not assign a universal standalone function to those keys. Firmware and menu context decide what happens. This workflow sends only the documented byte and never invents a response. Continue only when the connected subscriber displays an explicit prompt.",
                "Send documented key",
                "Cancel"))
        {
            return;
        }

        var firstEntryIndex = _session.Entries.Count;
        await _session.SendCommandAsync(command);
        await Task.Delay(TimeSpan.FromMilliseconds(650));

        while (true)
        {
            var response = await DisplayPromptAsync(
                $"{guide.Title} · next response",
                $"{RecentSubscriberResponse(firstEntryIndex)}\n\nEnter exactly the value requested by the live subscriber. Select Finish if no explicit input prompt is displayed. An empty response intentionally sends ENTER.",
                "Send response",
                "Finish",
                "Exact live-prompt response",
                200,
                Keyboard.Text,
                string.Empty);
            if (response is null)
            {
                _session.RecordActivity($"Guided firmware-key workflow finished · {guide.Title}");
                return;
            }

            if (!IsSingleLineAscii(response))
            {
                await DisplayAlertAsync(
                    "ASCII response required",
                    "Use no more than 200 single-line ASCII characters. Do not paste carriage returns or line feeds.",
                    "Try again");
                continue;
            }

            await _session.SendRawAsync(response);
            await Task.Delay(TimeSpan.FromMilliseconds(650));
            if (!await DisplayAlertAsync(
                    $"{guide.Title} · live response",
                    $"{RecentSubscriberResponse(firstEntryIndex)}\n\nDoes the connected subscriber explicitly request another value?",
                    "Enter next response",
                    "Finish"))
            {
                _session.RecordActivity($"Guided firmware-key workflow finished · {guide.Title}");
                return;
            }
        }
    }

    private async Task SendTextMessageAsync()
    {
        var message = await DisplayPromptAsync(
            "Send text message",
            "Enter up to 200 ASCII characters. The radio cannot perform normal transmit/receive work while text-entry mode is active, and incoming text is unavailable while a monitor is active.",
            "Review",
            "Cancel",
            "Message for the central station",
            200,
            Keyboard.Text,
            string.Empty);
        if (message is null)
        {
            return;
        }

        if (!IsSingleLineAscii(message) || message.Length == 0)
        {
            await DisplayAlertAsync(
                "Valid message required",
                "Enter 1–200 single-line ASCII characters.",
                "OK");
            return;
        }

        if (!await DisplayAlertAsync(
                "Transmit text message",
                $"Send this {message.Length}-character message through {_session.ConnectionName}? Confirm monitor modes are off and the account is on test when required by procedure.",
                "Send message",
                "Cancel"))
        {
            return;
        }

        await _session.RunConversationAsync(
            AesCommand.SendText,
            [new GuidedResponse(message, message)]);
        await DisplayAlertAsync(
            "Text entry sent",
            "The documented Ctrl+U command and the message were sent as one serialized workflow. Verify the subscriber response and central-station receipt.",
            "Done");
    }

    private string RecentSubscriberResponse(int firstEntryIndex)
    {
        var entries = _session.Entries
            .Skip(Math.Min(firstEntryIndex, _session.Entries.Count))
            .Where(entry => entry.Channel is "RX" or "SYSTEM")
            .TakeLast(8)
            .Select(entry => entry.Formatted)
            .ToArray();
        return entries.Length == 0
            ? "LIVE SUBSCRIBER RESPONSE\nNo new RX text has arrived. Do not guess a value."
            : $"LIVE SUBSCRIBER RESPONSE\n{string.Join(Environment.NewLine, entries)}";
    }

    private static bool IsSingleLineAscii(string value) =>
        value.Length <= 200 &&
        value.All(character => character <= 0x7f && character is not ('\r' or '\n'));

    private async Task ProgramIdentityAsync()
    {
        var idInput = await DisplayPromptAsync(
            "Subscriber ID",
            "Enter exactly four hexadecimal characters.",
            "Next",
            "Cancel",
            "1A2B",
            4,
            Keyboard.Text,
            TemplateSubscriberEntry.Text ?? _session.SubscriberId);
        if (idInput is null)
        {
            return;
        }

        var id = idInput.Trim().ToUpperInvariant();
        if (!IsFourHex(id))
        {
            await DisplayAlertAsync("Invalid subscriber ID", "Use exactly four characters 0–9 or A–F.", "OK");
            return;
        }

        var cipherInput = await DisplayPromptAsync(
            "System cipher",
            "Leave blank to preserve the existing cipher, or enter exactly four hexadecimal characters. The value is held only for this operation and is never logged or exported.",
            "Review",
            "Cancel",
            "Leave blank to preserve",
            4,
            Keyboard.Text,
            string.Empty);
        if (cipherInput is null)
        {
            return;
        }

        var cipher = cipherInput.Trim().ToUpperInvariant();
        if (cipher.Length > 0 && !IsFourHex(cipher))
        {
            await DisplayAlertAsync("Invalid cipher", "Leave it blank or use exactly four characters 0–9 or A–F.", "OK");
            return;
        }

        if (!await DisplayAlertAsync(
                "Confirm identity programming",
                $"Program subscriber ID {id} and {(cipher.Length == 0 ? "preserve the existing cipher" : "replace the cipher with the hidden value")}? Put the account on test first.",
                "Program",
                "Cancel"))
        {
            return;
        }

        _session.SetSubscriberId(id);
        if (cipher.Length > 0)
        {
            _session.RegisterSensitiveValue(cipher);
        }

        await _session.RunConversationAsync(
            AesCommand.ProgramIdCipher,
            [
                new GuidedResponse(id, id),
                new GuidedResponse(cipher, cipher.Length == 0 ? "<ENTER / preserve cipher>" : "[REDACTED CIPHER]", true)
            ]);
    }

    private async Task ProgramTimersAsync()
    {
        var hours = await PromptIntegerAsync("Check-in hours", "0–24; 00:00 is not allowed.", 0, 24, TemplateHoursEntry.Text ?? "24");
        var minutes = await PromptIntegerAsync("Check-in minutes", "0–59; 00:00 is not allowed.", 0, 59, TemplateMinutesEntry.Text ?? "00");
        if (hours is null || minutes is null || hours == 0 && minutes == 0)
        {
            return;
        }

        var acInput = await DisplayPromptAsync(
            "AC report delay",
            "Enter RM or a value from 0 through 60 minutes.",
            "Next",
            "Cancel",
            "RM",
            2,
            Keyboard.Text,
            TemplateAcDelayEntry.Text ?? "RM");
        if (acInput is null)
        {
            return;
        }

        var ac = acInput.Trim().ToUpperInvariant();
        if (!string.Equals(ac, "RM", StringComparison.Ordinal) &&
            (!int.TryParse(ac, NumberStyles.None, CultureInfo.InvariantCulture, out var acValue) ||
             acValue is < 0 or > 60))
        {
            await DisplayAlertAsync("Invalid AC delay", "Use RM or a value from 0 through 60.", "OK");
            return;
        }

        var delay = await PromptIntegerAsync(
            "Normal report delay",
            "Enter 0–330 seconds. Listed fire guidance identifies 10–20 seconds.",
            0,
            330,
            TemplateReportDelayEntry.Text ?? "10");
        if (delay is null)
        {
            return;
        }

        if (delay is < 10 or > 20 &&
            !await DisplayAlertAsync(
                "Non-standard fire delay",
                "This is outside the 10–20 second listed-fire guidance. Continue only when the approved design permits it.",
                "Continue",
                "Cancel"))
        {
            return;
        }

        await _session.RunConversationAsync(
            AesCommand.ProgramTimers,
            [
                new GuidedResponse(hours.Value.ToString("00", CultureInfo.InvariantCulture), hours.Value.ToString("00", CultureInfo.InvariantCulture)),
                new GuidedResponse(minutes.Value.ToString("00", CultureInfo.InvariantCulture), minutes.Value.ToString("00", CultureInfo.InvariantCulture)),
                new GuidedResponse(ac, ac),
                new GuidedResponse(delay.Value.ToString(CultureInfo.InvariantCulture), delay.Value.ToString(CultureInfo.InvariantCulture))
            ]);
    }

    private async Task ProgramZonesAsync()
    {
        var fireTrouble = await DisplayAlertAsync(
            "Fire/Trouble packets",
            "Enable Fire/Trouble packet interpretation?",
            "Yes",
            "No");
        var zonesInput = await DisplayPromptAsync(
            "Zone programming",
            $"Enter eight zone characters. Allowed: {(fireTrouble ? "F, S, B" : "S, B")}.",
            "Next",
            "Cancel",
            fireTrouble ? "FFFBBBBB" : "SSSBBBBB",
            8,
            Keyboard.Text,
            TemplateZonesEntry.Text ?? string.Empty);
        if (zonesInput is null)
        {
            return;
        }

        var zones = zonesInput.Trim().ToUpperInvariant();
        var allowed = fireTrouble ? "FSB" : "SB";
        if (zones.Length != 8 || zones.Any(value => !allowed.Contains(value, StringComparison.Ordinal)))
        {
            await DisplayAlertAsync("Invalid zones", $"Enter exactly eight {allowed} characters.", "OK");
            return;
        }

        var restoralsInput = await DisplayPromptAsync(
            "Restoral programming",
            "Enter eight R/X characters.",
            "Program",
            "Cancel",
            "RRRXXXXX",
            8,
            Keyboard.Text,
            TemplateRestoralsEntry.Text ?? string.Empty);
        if (restoralsInput is null)
        {
            return;
        }

        var restorals = restoralsInput.Trim().ToUpperInvariant();
        if (restorals.Length != 8 || restorals.Any(value => value is not ('R' or 'X')))
        {
            await DisplayAlertAsync("Invalid restorals", "Enter exactly eight R/X characters.", "OK");
            return;
        }

        await _session.RunConversationAsync(
            AesCommand.ProgramZones,
            [
                new GuidedResponse(fireTrouble ? "Y" : "N", fireTrouble ? "Y" : "N"),
                new GuidedResponse(zones, zones),
                new GuidedResponse(restorals, restorals)
            ]);
        await DisplayAlertAsync(
            "Physical board RESET required",
            "Press the physical RESET button on the AES board so zone states reinitialize. Do not use Reset RAM for this step.",
            "OK");
    }

    private async Task ProgramModesAsync()
    {
        var repeating = await DisplayAlertAsync("Repeating", "Enable repeating?", "Yes", "No");
        var suppressAc = await DisplayAlertAsync("AC failure", "Suppress AC-failure reports?", "Suppress", "Report");
        var responses = new List<GuidedResponse>
        {
            new(repeating ? "Y" : "N", repeating ? "Y" : "N"),
            new(suppressAc ? "Y" : "N", suppressAc ? "Y" : "N")
        };

        if (_session.SelectedModel == "7788F")
        {
            var suppressCharger = await DisplayAlertAsync("Charger fault", "Suppress charger-fault reports?", "Suppress", "Report");
            var suppressGround = await DisplayAlertAsync("Ground fault", "Suppress ground-fault reports?", "Suppress", "Report");
            responses.Add(new GuidedResponse(suppressCharger ? "Y" : "N", suppressCharger ? "Y" : "N"));
            responses.Add(new GuidedResponse(suppressGround ? "Y" : "N", suppressGround ? "Y" : "N"));
        }

        if (responses.Skip(1).Any(response => response.Value == "Y") &&
            !await DisplayAlertAsync(
                "Trouble reporting will be suppressed",
                "Listed fire settings normally require every suppression value to be N. Continue only when the approved design requires suppression.",
                "Continue",
                "Cancel"))
        {
            return;
        }

        await _session.RunConversationAsync(AesCommand.ProgramModes, responses);
    }

    private async Task ResetRamAsync()
    {
        var typed = await DisplayPromptAsync(
            "Reset RAM",
            "This factory-defaults timers, zones, restorals, and modes. ID and cipher remain. Type RESET RAM exactly.",
            "Review",
            "Cancel",
            "RESET RAM");
        if (!string.Equals(typed?.Trim(), "RESET RAM", StringComparison.OrdinalIgnoreCase) ||
            !await DisplayAlertAsync(
                "Final destructive-action confirmation",
                "Have you recorded the current configuration, placed the account on test, and confirmed that Reset RAM is required?",
                "Reset RAM",
                "Cancel"))
        {
            return;
        }

        await _session.RunConversationAsync(
            AesCommand.ResetRam,
            [new GuidedResponse("Y", "Y")]);
    }

    private async Task KeyTransmitterAsync()
    {
        if (!await DisplayAlertAsync(
                "RF load confirmation",
                "Is the correct antenna or approved dummy load attached, with safe separation and the account on test?",
                "Confirmed",
                "Cancel") ||
            !await DisplayAlertAsync(
                "Key transmitter",
                "This can cause a real RF transmission in physical mode. Send the key command now?",
                "Key briefly",
                "Cancel"))
        {
            return;
        }

        await _session.SendCommandAsync(AesCommand.KeyTransmitter);
    }

    private async Task<bool> EnsureConnectedAsync()
    {
        if (_session.IsConnected)
        {
            return true;
        }

        if (!await DisplayAlertAsync(
                "Connect transport",
                $"Connect to {_session.ConnectionName}? Simulation should remain the default until the Android FTDI path passes the physical bench checklist.",
                "Connect",
                "Cancel"))
        {
            return false;
        }

        await _session.ConnectAsync();
        return _session.IsConnected;
    }

    private async Task<int?> PromptIntegerAsync(
        string title,
        string message,
        int minimum,
        int maximum,
        string initial)
    {
        var value = await DisplayPromptAsync(
            title,
            message,
            "Next",
            "Cancel",
            initial,
            -1,
            Keyboard.Numeric,
            initial);
        if (value is null)
        {
            return null;
        }

        if (!int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var parsed) ||
            parsed < minimum ||
            parsed > maximum)
        {
            await DisplayAlertAsync("Invalid value", $"Enter a whole number from {minimum} through {maximum}.", "OK");
            return null;
        }

        return parsed;
    }

    private async Task ReloadTemplatesAsync()
    {
        _templates.Clear();
        _templates.AddRange(await _templateStore.LoadAsync());
        RefreshTemplatePicker();
        if (_templates.Count > 0)
        {
            TemplatePicker.SelectedIndex = 0;
            ApplyTemplate(_templates[0]);
        }
    }

    private void RefreshTemplatePicker()
    {
        TemplatePicker.ItemsSource = null;
        TemplatePicker.ItemsSource = _templates.ToArray();
    }

    private void OnTemplateSelected(object? sender, EventArgs args)
    {
        if (TemplatePicker.SelectedItem is ProgrammingTemplate template)
        {
            ApplyTemplate(template);
        }
    }

    private async void OnSaveTemplateClicked(object? sender, EventArgs args)
    {
        var template = await ReadTemplateAsync();
        if (template is null)
        {
            return;
        }

        _templates.RemoveAll(value => string.Equals(value.Name, template.Name, StringComparison.OrdinalIgnoreCase));
        _templates.Add(template);
        await _templateStore.SaveAsync(_templates);
        RefreshTemplatePicker();
        TemplatePicker.SelectedItem = _templates.First(value => value.Name == template.Name);
        _session.RecordActivity($"Programming template saved · {template.Name} · cipher excluded");
    }

    private void OnApplyTemplateClicked(object? sender, EventArgs args)
    {
        if (TemplatePicker.SelectedItem is not ProgrammingTemplate template)
        {
            return;
        }

        ApplyTemplate(template);
        _session.SelectModel(template.Model == AesModel.Aes7744F ? "7744F" : "7788F");
        if (IsFourHex(template.SubscriberId))
        {
            _session.SetSubscriberId(template.SubscriberId);
        }

        _session.RecordActivity($"Programming template applied · {template.Name}");
    }

    private async void OnDeleteTemplateClicked(object? sender, EventArgs args)
    {
        if (TemplatePicker.SelectedItem is not ProgrammingTemplate template ||
            !await DisplayAlertAsync("Delete template", $"Delete “{template.Name}”?", "Delete", "Cancel"))
        {
            return;
        }

        _templates.Remove(template);
        await _templateStore.SaveAsync(_templates);
        RefreshTemplatePicker();
        TemplatePicker.SelectedIndex = _templates.Count > 0 ? 0 : -1;
        _session.RecordActivity($"Programming template deleted · {template.Name}");
    }

    private async void OnImportTemplatesClicked(object? sender, EventArgs args)
    {
        try
        {
            var selected = await FilePicker.Default.PickAsync(
                new PickOptions { PickerTitle = "Import Superior AES template JSON" });
            if (selected is null)
            {
                return;
            }

            await using var stream = await selected.OpenReadAsync();
            var imported = await JsonSerializer.DeserializeAsync<List<ProgrammingTemplate>>(stream, JsonOptions);
            if (imported is null || imported.Count == 0)
            {
                throw new InvalidDataException("No programming templates were found.");
            }

            _templates.Clear();
            _templates.AddRange(imported);
            await _templateStore.SaveAsync(_templates);
            RefreshTemplatePicker();
            TemplatePicker.SelectedIndex = 0;
            _session.RecordActivity($"Programming templates imported · {_templates.Count} template(s) · cipher schema excluded");
        }
        catch (Exception exception) when (exception is IOException or JsonException or UnauthorizedAccessException or InvalidDataException)
        {
            await DisplayAlertAsync("Import failed", exception.Message, "OK");
        }
    }

    private async void OnExportTemplatesClicked(object? sender, EventArgs args)
    {
        var directory = IOPath.Combine(FileSystem.Current.AppDataDirectory, "Exports");
        Directory.CreateDirectory(directory);
        var path = IOPath.Combine(directory, $"Superior-AES-Templates-{DateTime.Now:yyyyMMdd-HHmmss}.json");
        await File.WriteAllTextAsync(path, JsonSerializer.Serialize(_templates, JsonOptions));
        _session.RecordActivity($"Programming templates exported · {_templates.Count} template(s) · cipher excluded");
        await Share.Default.RequestAsync(
            new ShareFileRequest("Superior AES programming templates", new ShareFile(path)));
    }

    private async Task<ProgrammingTemplate?> ReadTemplateAsync()
    {
        var name = TemplateNameEntry.Text?.Trim() ?? string.Empty;
        var id = TemplateSubscriberEntry.Text?.Trim().ToUpperInvariant() ?? string.Empty;
        if (name.Length == 0 || !IsFourHex(id) ||
            !int.TryParse(TemplateHoursEntry.Text, out var hours) ||
            !int.TryParse(TemplateMinutesEntry.Text, out var minutes) ||
            !int.TryParse(TemplateReportDelayEntry.Text, out var reportDelay) ||
            !int.TryParse(TemplateContactGainEntry.Text, out var contactGain) ||
            contactGain is < 1 or > 16 ||
            !int.TryParse(TemplateContactTtlHoursEntry.Text, out var contactTtlHours) ||
            contactTtlHours is < 0 or > 99 ||
            !int.TryParse(TemplateContactTtlMinutesEntry.Text, out var contactTtlMinutes) ||
            contactTtlMinutes is < 0 or > 59)
        {
            await DisplayAlertAsync(
                "Template values need attention",
                "Enter a name, four-hex subscriber ID, and valid numeric HH, MM, and report-delay values.",
                "OK");
            return null;
        }

        return new ProgrammingTemplate(
            name,
            TemplateModelPicker.SelectedIndex == 0 ? AesModel.Aes7744F : AesModel.Aes7788F,
            id,
            hours,
            minutes,
            TemplateAcDelayEntry.Text?.Trim().ToUpperInvariant() ?? "RM",
            reportDelay,
            TemplateFireTroubleSwitch.IsToggled,
            TemplateZonesEntry.Text?.Trim().ToUpperInvariant() ?? string.Empty,
            TemplateRestoralsEntry.Text?.Trim().ToUpperInvariant() ?? string.Empty,
            TemplateRepeatingSwitch.IsToggled,
            TemplateSuppressAcSwitch.IsToggled,
            TemplateSuppressChargerSwitch.IsToggled,
            TemplateSuppressGroundSwitch.IsToggled,
            TemplateAntennaPicker.SelectedItem?.ToString() ?? AntennaCatalog.All[0].DisplayName,
            TemplateNotesEditor.Text ?? string.Empty,
            TemplateContactIdPicker.SelectedItem?.ToString() ?? "None",
            TemplateContactFormatPicker.SelectedItem?.ToString() ?? "Contact ID (C)",
            TemplateContactInterceptEntry.Text?.Trim() ?? "555",
            TemplateContactPhoneLinePicker.SelectedItem?.ToString() ?? "Match approved system design",
            contactGain,
            (TemplateContactFourXxEntry.Text?.Trim().ToUpperInvariant() ?? "U"),
            contactTtlHours,
            contactTtlMinutes,
            TemplateContactBlindDialSwitch.IsToggled);
    }

    private void ApplyTemplate(ProgrammingTemplate template)
    {
        TemplateNameEntry.Text = template.Name;
        TemplateSubscriberEntry.Text = template.SubscriberId;
        TemplateModelPicker.SelectedIndex = template.Model == AesModel.Aes7744F ? 0 : 1;
        TemplateHoursEntry.Text = template.CheckInHours.ToString(CultureInfo.InvariantCulture);
        TemplateMinutesEntry.Text = template.CheckInMinutes.ToString("00", CultureInfo.InvariantCulture);
        TemplateAcDelayEntry.Text = template.AcReportDelay;
        TemplateReportDelayEntry.Text = template.ReportDelaySeconds.ToString(CultureInfo.InvariantCulture);
        TemplateZonesEntry.Text = template.ZoneConfiguration;
        TemplateRestoralsEntry.Text = template.RestoralConfiguration;
        TemplateFireTroubleSwitch.IsToggled = template.FireTroubleEnabled;
        TemplateRepeatingSwitch.IsToggled = template.RepeatingEnabled;
        TemplateSuppressAcSwitch.IsToggled = template.SuppressAcFailure;
        TemplateSuppressChargerSwitch.IsToggled = template.SuppressChargerFault;
        TemplateSuppressGroundSwitch.IsToggled = template.SuppressGroundFault;
        TemplateAntennaPicker.SelectedItem = AntennaCatalog.All
            .Select(option => option.DisplayName)
            .FirstOrDefault(value => string.Equals(value, template.Antenna, StringComparison.OrdinalIgnoreCase))
            ?? AntennaCatalog.All[0].DisplayName;
        TemplateContactIdPicker.SelectedItem = TemplateContactIdPicker.ItemsSource
            .Cast<string>()
            .FirstOrDefault(value => string.Equals(value, template.DialerCaptureModule, StringComparison.OrdinalIgnoreCase))
            ?? "None";
        TemplateContactFormatPicker.SelectedItem = TemplateContactFormatPicker.ItemsSource
            .Cast<string>()
            .FirstOrDefault(value => string.Equals(value, template.ContactIdReportFormat, StringComparison.OrdinalIgnoreCase))
            ?? "Contact ID (C)";
        TemplateContactInterceptEntry.Text = template.ContactIdInterceptNumber;
        TemplateContactPhoneLinePicker.SelectedItem = TemplateContactPhoneLinePicker.ItemsSource
            .Cast<string>()
            .FirstOrDefault(value => string.Equals(value, template.ContactIdPhoneLineMode, StringComparison.OrdinalIgnoreCase))
            ?? "Match approved system design";
        TemplateContactGainEntry.Text = template.ContactIdInputGain.ToString(CultureInfo.InvariantCulture);
        TemplateContactFourXxEntry.Text = template.ContactIdFourXxLetter;
        TemplateContactTtlHoursEntry.Text = template.ContactIdTtlHours.ToString(CultureInfo.InvariantCulture);
        TemplateContactTtlMinutesEntry.Text = template.ContactIdTtlMinutes.ToString(CultureInfo.InvariantCulture);
        TemplateContactBlindDialSwitch.IsToggled = template.ContactIdBlindDialEnabled;
        TemplateNotesEditor.Text = template.Notes;
    }

    private static bool IsFourHex(string value) =>
        value.Length == 4 && value.All(character => Uri.IsHexDigit(character));
}
