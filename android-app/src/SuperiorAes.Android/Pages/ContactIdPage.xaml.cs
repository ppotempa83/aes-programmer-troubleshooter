using Microsoft.Maui.ApplicationModel;
using SuperiorAes.Android.Services;
using SuperiorAes.Core.Protocol;

namespace SuperiorAes.Android.Pages;

public partial class ContactIdPage : ContentPage
{
    private readonly ICompanionSession _session;

    public ContactIdPage(ICompanionSession session)
    {
        InitializeComponent();
        _session = session;
        ModulePicker.ItemsSource = new[]
        {
            "7794 IntelliPro Fire (recommended for legacy 7744F/7788F)",
            "7067 IntelliTap II (historical / discontinued)",
            "7794A IntelliPro (7707/7177 IntelliNet 2.0 Fire only)"
        };
        ReportFormatPicker.ItemsSource = new[] { "Contact ID (C)", "Pulse", "Modem IIe / IIIa2", "Other approved format" };
        PhoneLinePicker.ItemsSource = new[] { "Match approved system design", "Primary line", "Secondary line", "No line monitoring" };
        InputGainPicker.ItemsSource = Enumerable.Range(1, 16).Select(value => value.ToString()).ToArray();
        FourXxPicker.ItemsSource = new[] { "U", "A", "B", "C", "D", "E", "F" };
        IntelliTapFormatPicker.ItemsSource = new[]
        {
            "Contact ID · jumper 1 only",
            "3+1 / 4+1 / 4+2, 1400 Hz · jumper 2 only",
            "3+1 / 4+1 / 4+2, 2300 Hz · jumper 3 only"
        };
        IntelliTapPhonePicker.ItemsSource = new[]
        {
            "3-5-* · jumper 5 installed (factory default / preferred)",
            "5-5-5 · jumper 5 removed"
        };
        IntelliTapLineModePicker.ItemsSource = new[]
        {
            "Phone line attached, no line-cut report · no jumpers 6, 7, or 8",
            "1-minute line-cut delay · jumper 7 only",
            "2-minute line-cut delay · jumper 6 only",
            "3-minute line-cut delay · jumpers 6 and 7 only",
            "No phone line / no line-cut report · jumper 8 only"
        };
        ModulePicker.SelectedIndex = 0;
        ReportFormatPicker.SelectedIndex = 0;
        PhoneLinePicker.SelectedIndex = 0;
        InputGainPicker.SelectedItem = "10";
        FourXxPicker.SelectedIndex = 0;
        IntelliTapFormatPicker.SelectedIndex = 0;
        IntelliTapPhonePicker.SelectedIndex = 0;
        IntelliTapLineModePicker.SelectedIndex = 0;
        _session.StateChanged += OnSessionChanged;
        UpdateModuleStatus();
    }

    private void OnSessionChanged(object? sender, EventArgs args) =>
        MainThread.BeginInvokeOnMainThread(() =>
        {
            TerminalPreviewLabel.Text = string.Join(
                Environment.NewLine,
                _session.Entries
                    .Where(entry => entry.Channel is "RX" or "TX")
                    .TakeLast(24)
                    .Select(entry => entry.Formatted));
        });

    private void OnModuleChanged(object? sender, EventArgs args)
    {
        UpdateModuleStatus();
        _session.RecordActivity($"Contact ID accessory selection changed · index {ModulePicker.SelectedIndex + 1}");
    }

    private void UpdateModuleStatus()
    {
        ModuleStatusLabel.Text = ModulePicker.SelectedIndex switch
        {
            0 => "Recommended legacy-fire Contact ID path for 7744F and 7788F. Use the official 7794 manual.",
            1 => "Historical 7744F/7788F option. AES lists 7067 IntelliTap II as discontinued and unsupported; verify exact hardware, listing, receiver, and AHJ requirements.",
            2 => "7794A is only for compatible 7707/7177 IntelliNet 2.0 Fire subscribers. Do not install it in a legacy 7744F or 7788F.",
            _ => "Select the exact accessory."
        };
        IntelliTapWorkflowPanel.IsEnabled = ModulePicker.SelectedIndex == 1;
        IntelliTapWorkflowPanel.Opacity = IntelliTapWorkflowPanel.IsEnabled ? 1 : 0.62;
    }

    private void OnSaveWorksheetClicked(object? sender, EventArgs args)
    {
        _session.RecordActivity(
            $"Contact ID worksheet recorded · accessory option {ModulePicker.SelectedIndex + 1} · " +
            $"format {ReportFormatPicker.SelectedItem} · intercept {Safe(InterceptEntry.Text)} · " +
            $"line mode {PhoneLinePicker.SelectedItem} · gain {InputGainPicker.SelectedItem} · " +
            $"4XX {FourXxPicker.SelectedItem} · TTL {Safe(TtlHoursEntry.Text)}:{Safe(TtlMinutesEntry.Text)} · " +
            $"blind dial {(BlindDialCheckBox.IsChecked ? "enabled" : "disabled")}");
        ResultLabel.Text = "Worksheet values were added to the timestamped session activity. No cipher or API key is included.";
    }

    private async void OnIntelliTapChecklistClicked(object? sender, EventArgs args)
    {
        if (ModulePicker.SelectedIndex != 1)
        {
            await DisplayAlertAsync(
                "Select 7067 IntelliTap II",
                "Select the historical 7067 IntelliTap II accessory before recording its physical jumper plan.",
                "OK");
            return;
        }

        var confirmations = new[]
        {
            (Name: "manual/revision verification", IsChecked: IntelliTapManualCheckBox.IsChecked),
            (Name: "power and telephone-line isolation", IsChecked: IntelliTapPowerCheckBox.IsChecked),
            (Name: "official-figure wiring verification", IsChecked: IntelliTapWiringCheckBox.IsChecked),
            (Name: "supplemental subscriber-zone trigger", IsChecked: IntelliTapZoneCheckBox.IsChecked),
            (Name: "tone dialing and answer number", IsChecked: IntelliTapPanelCheckBox.IsChecked),
            (Name: "7067 reset after jumper changes", IsChecked: IntelliTapResetCheckBox.IsChecked),
            (Name: "account test and acceptance plan", IsChecked: IntelliTapTestCheckBox.IsChecked)
        };
        var outstanding = confirmations
            .Where(item => !item.IsChecked)
            .Select(item => item.Name)
            .ToArray();
        var plan = BuildIntelliTapPlan();

        if (outstanding.Length > 0)
        {
            IntelliTapOutputLabel.Text =
                $"{plan}\n\nNOT READY — outstanding confirmations:\n- " +
                string.Join("\n- ", outstanding);
            _session.RecordActivity(
                $"7067 IntelliTap jumper-plan review incomplete · {outstanding.Length} confirmation(s) outstanding");
            await DisplayAlertAsync(
                "7067 plan is not ready",
                "Complete every physical safety, official-manual, alarm-panel, reset, and acceptance confirmation. No serial command was sent.",
                "OK");
            return;
        }

        if (!await DisplayAlertAsync(
                "Record physical 7067 plan",
                $"{plan}\n\nThis records a worksheet only. It sends no PC, FTDI, subscriber, or 7794 J2 command. Perform the work only from the exact original manual and approved system design.",
                "Record plan",
                "Cancel"))
        {
            return;
        }

        IntelliTapOutputLabel.Text =
            $"{plan}\n\nREADY FOR MANUAL EXECUTION — no serial command was sent. After changing jumpers, press the 7067 Reset switch and complete central-station acceptance.";
        _session.RecordActivity(
            $"7067 IntelliTap physical jumper plan recorded · format option {IntelliTapFormatPicker.SelectedIndex + 1} · answer-number option {IntelliTapPhonePicker.SelectedIndex + 1} · line-mode option {IntelliTapLineModePicker.SelectedIndex + 1} · no serial command sent");
    }

    private string BuildIntelliTapPlan()
    {
        var lineSafety = IntelliTapLineModePicker.SelectedIndex switch
        {
            0 => "Positions 6, 7, and 8 all open.",
            1 => "Position 7 only; positions 6 and 8 open.",
            2 => "Position 6 only; positions 7 and 8 open.",
            3 => "Positions 6 and 7; position 8 open.",
            4 => "Position 8 only; positions 6 and 7 open. Never connect a phone line in this mode.",
            _ => "Select one documented line mode."
        };
        return string.Join(
            Environment.NewLine,
            "7067 PHYSICAL JUMPER PLAN · AES Doc #40-7067B",
            $"Format: {IntelliTapFormatPicker.SelectedItem}",
            $"Answer number: {IntelliTapPhonePicker.SelectedItem}",
            $"Phone-line mode: {IntelliTapLineModePicker.SelectedItem}",
            $"Conflict check: {lineSafety}",
            "Positions 4 and 10 are unused. Position 9 is AES debug only and must remain open.",
            "Use the original manual figures for every mounting, serial/power ribbon, dialer/telephone, zone, and error-output connection.",
            "Programming method: physical jumpers + alarm-panel tone-dial settings + 7067 Reset switch. No PC/FTDI serial command.");
    }

    private async void OnControlClicked(object? sender, EventArgs args)
    {
        if (sender is not Button { CommandParameter: string control } ||
            !await ValidateLiveIntelliProAsync())
        {
            return;
        }

        var explanation = control switch
        {
            "F1" => "Enter the 7794 CONFIG menu. No typed input. Read the first live option.",
            "F3" => "Change the displayed 7794 option. Example: cycle report format until Contact ID (C) is displayed.",
            "F4" => "Move up through 7794 configuration options.",
            "F5" => "Move down through 7794 configuration options.",
            "ESC" => "Exit the active 7794 configuration menu. ESC is sent as raw 0x1B without carriage return.",
            _ => string.Empty
        };
        if (explanation.Length == 0 ||
            !await DisplayAlertAsync(
                $"7794 control · {control}",
                $"{explanation}\n\nSend through the verified J2 connection now?",
                "Send",
                "Cancel"))
        {
            return;
        }

        try
        {
            switch (control)
            {
                case "F1":
                    await _session.SendCommandAsync(AesCommand.Function1);
                    break;
                case "F3":
                    await _session.SendCommandAsync(AesCommand.Function3);
                    break;
                case "F4":
                    await _session.SendCommandAsync(AesCommand.RoutingTable);
                    break;
                case "F5":
                    await _session.SendCommandAsync(AesCommand.SendText);
                    break;
                case "ESC":
                    await _session.SendEscapeAsync();
                    break;
            }
        }
        catch (Exception exception) when (exception is InvalidOperationException or IOException or TimeoutException)
        {
            await DisplayAlertAsync("7794 command failed", exception.Message, "OK");
        }
    }

    private async void OnSendLineClicked(object? sender, EventArgs args)
    {
        if (!await ValidateLiveIntelliProAsync())
        {
            return;
        }

        var value = TerminalEntry.Text ?? string.Empty;
        TerminalEntry.Text = string.Empty;
        try
        {
            await _session.SendRawAsync(value);
        }
        catch (Exception exception) when (exception is InvalidOperationException or IOException or TimeoutException)
        {
            await DisplayAlertAsync("7794 response failed", exception.Message, "OK");
        }
    }

    private async Task<bool> ValidateLiveIntelliProAsync()
    {
        if (ModulePicker.SelectedIndex != 0)
        {
            await DisplayAlertAsync(
                "Select 7794 IntelliPro Fire",
                "Live controls are only mapped for the 7794 J2 workflow. IntelliTap jumper settings are historical and are not automated; 7794A is a different IntelliNet 2.0 product.",
                "OK");
            return false;
        }

        if (!J2VerifiedCheckBox.IsChecked)
        {
            await DisplayAlertAsync(
                "J2 safety confirmation required",
                "Verify the 7794 J2 HandHeld connection, isolate subscriber J1 pin 6, and place the account on test.",
                "OK");
            return false;
        }

        if (!_session.IsConnected)
        {
            await _session.ConnectAsync();
        }

        return _session.IsConnected;
    }

    private void OnCaptureTestClicked(object? sender, EventArgs args)
    {
        var completed = new[]
        {
            (Name: "alarm", IsChecked: AlarmCheckBox.IsChecked),
            (Name: "trouble", IsChecked: TroubleCheckBox.IsChecked),
            (Name: "supervisory", IsChecked: SupervisoryCheckBox.IsChecked),
            (Name: "restoral", IsChecked: RestoralCheckBox.IsChecked)
        };
        var passed = completed.Where(item => item.IsChecked).Select(item => item.Name).ToArray();
        var missing = completed.Where(item => !item.IsChecked).Select(item => item.Name).ToArray();
        ResultLabel.Text = missing.Length == 0
            ? "All representative Contact ID event classes were documented as confirmed. Complete the normal return-to-service process."
            : $"Partial test recorded. Still required: {string.Join(", ", missing)}.";
        _session.RecordActivity(
            $"Contact ID capture test documented · confirmed {Value(passed)} · outstanding {Value(missing)}");
    }

    private async void OnManualClicked(object? sender, EventArgs args)
    {
        if (sender is not Button { CommandParameter: string fileName })
        {
            return;
        }

        try
        {
            var path = await PackagedAssetService.MaterializeAsync($"Training/{Path.GetFileName(fileName)}");
            _session.RecordActivity($"Training manual shared · {Path.GetFileName(fileName)}");
            await Share.Default.RequestAsync(
                new ShareFileRequest("Superior AES training manual", new ShareFile(path)));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            await DisplayAlertAsync("Manual unavailable", exception.Message, "OK");
        }
    }

    private static string Safe(string? value) =>
        string.IsNullOrWhiteSpace(value) ? "not entered" : value.Trim().Replace("\r", " ").Replace("\n", " ");

    private static string Value(IReadOnlyCollection<string> values) =>
        values.Count == 0 ? "none" : string.Join(", ", values);
}
