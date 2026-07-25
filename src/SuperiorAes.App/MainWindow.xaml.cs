using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
using SuperiorAes.Core.Connections;
using SuperiorAes.Core.Diagnostics;
using SuperiorAes.Core.Models;
using SuperiorAes.Core.Protocol;
using SuperiorAes.Core.Reporting;
using SuperiorAes.Core.Simulation;
using SuperiorAes.Core.SiteAnalysis;
using SuperiorAes.Core.Templates;
using Ellipse = System.Windows.Shapes.Ellipse;
using Line = System.Windows.Shapes.Line;

namespace SuperiorAes.App;

public partial class MainWindow : Window
{
    private static readonly string[] PageTitles =
    [
        "Dashboard",
        "Program subscriber",
        "Status & routes",
        "RF monitor",
        "Guided troubleshooter",
        "Site survey",
        "New radio check",
        "Virtual mesh simulator",
        "Training & documentation",
        "Field report",
        "Raw terminal"
    ];

    private static readonly string[] PageSubtitles =
    [
        "Connect, inspect, program, diagnose, and document.",
        "Guided access to the official legacy AES programming functions.",
        "Read enrollment, self-test, network paths, and zone inputs.",
        "Capture live receive, transmit, and nearby mesh traffic.",
        "Turn subscriber evidence into an ordered field diagnosis.",
        "Compare antenna locations, RF power, voltage, and acknowledgement results.",
        "Compare an address, building evidence, and public AES coverage layers.",
        "Configure up to four virtual radios and simulate TX, RX, repeating, ACKs, and drops.",
        "Read and search the uploaded technician training guides inside the app.",
        "Export a printable service record with the complete evidence trail.",
        "Direct ASCII access for advanced and firmware-specific operations."
    ];

    private readonly ObservableCollection<RouteEntry> _routes = [];
    private readonly ObservableCollection<ZoneState> _zones = [];
    private readonly ObservableCollection<DiagnosticFinding> _findings = [];
    private readonly ObservableCollection<SiteSurveyTrial> _surveyTrials = [];
    private readonly ObservableCollection<ProgrammingTemplate> _programmingTemplates = [];
    private readonly ObservableCollection<VirtualRadio> _virtualRadios = [];
    private readonly ObservableCollection<VirtualMeshSignal> _meshEvents = [];
    private readonly StringBuilder _transcript = new();
    private readonly StringBuilder _parseBuffer = new();
    private readonly Emergency24CoverageService _coverageService = new();
    private readonly GoogleSiteDataService _googleSiteDataService = new();
    private readonly VirtualMeshSimulator _meshSimulator = new();
    private readonly ProgrammingTemplateStore _templateStore;
    private AesProtocolClient? _client;
    private AesLocalStatus? _lastStatus;
    private AesMapCoverageResult? _mappedCoverage;
    private AesMapCoverageAnalysis? _lastCoverageAnalysis;
    private BuildingSiteData? _lastBuildingData;
    private RadioSiteRecommendation? _lastRadioRecommendation;
    private bool _receiveMonitorEnabled;
    private bool _transmitMonitorEnabled;
    private bool _monitorAllEnabled;
    private bool _isClosing;
    private int _trainingSearchStart;

    private static readonly IReadOnlyList<TrainingGuide> TrainingGuides =
    [
        new(
            "Complete Technician Guide",
            "AES-7744F-7788F-Complete-Technician-Guide.pdf",
            "AES-7744F-7788F-Complete-Technician-Guide.txt",
            "complete-guide-cover.png"),
        new(
            "NETCON, Signal Survey & Antenna Guide",
            "AES-7744F-7788F-NETCON-Signal-Survey-and-Antenna-Guide.pdf",
            "AES-7744F-7788F-NETCON-Signal-Survey-and-Antenna-Guide.txt",
            "netcon-guide-cover.png"),
        new(
            "US232R Wiring & Commands",
            "AES-7744F-7788F-US232R-Wiring-and-Commands.pdf",
            "AES-7744F-7788F-US232R-Wiring-and-Commands.txt",
            "wiring-guide-cover.png")
    ];

    public MainWindow()
    {
        var templatePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "SuperiorFire",
            "AES Programmer",
            "templates.json");
        _templateStore = new ProgrammingTemplateStore(templatePath);

        InitializeComponent();
        RoutesGrid.ItemsSource = _routes;
        ZonesGrid.ItemsSource = _zones;
        FindingsList.ItemsSource = _findings;
        SurveyGrid.ItemsSource = _surveyTrials;
        ProgrammingTemplateCombo.ItemsSource = _programmingTemplates;
        ProgrammingAntennaCombo.ItemsSource = AntennaCatalog.All;
        SurveyAntennaCombo.ItemsSource = AntennaCatalog.All;
        ProgrammingAntennaCombo.SelectedIndex = 0;
        SurveyAntennaCombo.SelectedIndex = 0;
        VirtualRadiosGrid.ItemsSource = _virtualRadios;
        MeshSourceCombo.ItemsSource = _virtualRadios;
        MeshEventsGrid.ItemsSource = _meshEvents;
        TrainingGuideCombo.ItemsSource = TrainingGuides;
        TrainingGuideCombo.DisplayMemberPath = nameof(TrainingGuide.Title);
        GoogleApiKeyPasswordBox.Password = Environment.GetEnvironmentVariable("GOOGLE_MAPS_API_KEY") ?? string.Empty;
        MeshCanvas.SizeChanged += (_, _) => DrawMesh();
        Loaded += MainWindow_Loaded;
        RefreshPorts();
        UpdateConnectionUi();
    }

    private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        await LoadTemplatesAsync();
        AddStarterMeshRadios();
        TrainingGuideCombo.SelectedIndex = 0;
    }

    private AesModel SelectedModel =>
        ModelCombo.SelectedIndex == 0 ? AesModel.Aes7744F : AesModel.Aes7788F;

    private async Task LoadTemplatesAsync()
    {
        try
        {
            ReplaceCollection(_programmingTemplates, await _templateStore.LoadAsync());
            ProgrammingTemplateCombo.SelectedIndex = _programmingTemplates.Count > 0 ? 0 : -1;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            ReplaceCollection(_programmingTemplates, ProgrammingTemplate.Defaults);
            StatusBarText.Text = $"Template store unavailable; defaults loaded: {exception.Message}";
        }
    }

    private async void SaveTemplate_Click(object sender, RoutedEventArgs e)
    {
        var name = TemplateNameTextBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            ShowWarning("Enter a template name.");
            return;
        }

        if (!TryReadProgrammingValues(out var template, name))
        {
            return;
        }

        var existing = _programmingTemplates.FirstOrDefault(value =>
            string.Equals(value.Name, name, StringComparison.OrdinalIgnoreCase));
        if (existing is not null)
        {
            _programmingTemplates.Remove(existing);
        }

        _programmingTemplates.Add(template);
        await SaveTemplatesAsync();
        ProgrammingTemplateCombo.SelectedItem = template;
        StatusBarText.Text = $"Programming template “{name}” saved. Cipher was not stored.";
    }

    private void ApplyTemplate_Click(object sender, RoutedEventArgs e)
    {
        if (ProgrammingTemplateCombo.SelectedItem is not ProgrammingTemplate template)
        {
            ShowWarning("Choose a programming template first.");
            return;
        }

        ModelCombo.SelectedIndex = template.Model == AesModel.Aes7744F ? 0 : 1;
        SubscriberIdTextBox.Text = template.SubscriberId;
        CheckHoursTextBox.Text = template.CheckInHours.ToString(CultureInfo.InvariantCulture);
        CheckMinutesTextBox.Text = template.CheckInMinutes.ToString("00", CultureInfo.InvariantCulture);
        AcDelayTextBox.Text = template.AcReportDelay;
        ReportDelayTextBox.Text = template.ReportDelaySeconds.ToString(CultureInfo.InvariantCulture);
        FireTroubleCombo.SelectedIndex = template.FireTroubleEnabled ? 0 : 1;
        ZoneConfigTextBox.Text = template.ZoneConfiguration;
        RestoralConfigTextBox.Text = template.RestoralConfiguration;
        RepeatingCheckBox.IsChecked = template.RepeatingEnabled;
        SuppressAcCheckBox.IsChecked = template.SuppressAcFailure;
        SuppressChargerCheckBox.IsChecked = template.SuppressChargerFault;
        SuppressGroundCheckBox.IsChecked = template.SuppressGroundFault;
        ProgrammingAntennaCombo.SelectedItem = AntennaCatalog.All.FirstOrDefault(
            option => string.Equals(option.DisplayName, template.Antenna, StringComparison.OrdinalIgnoreCase))
            ?? AntennaCatalog.All[0];
        CipherTextBox.Clear();
        TemplateNameTextBox.Text = template.Name;
        StatusBarText.Text = $"Template “{template.Name}” applied. Existing cipher is preserved.";
    }

    private async void DeleteTemplate_Click(object sender, RoutedEventArgs e)
    {
        if (ProgrammingTemplateCombo.SelectedItem is not ProgrammingTemplate template)
        {
            ShowWarning("Choose a programming template to delete.");
            return;
        }

        if (MessageBox.Show(
                this,
                $"Delete the template “{template.Name}”?",
                "Delete programming template",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question) != MessageBoxResult.Yes)
        {
            return;
        }

        _programmingTemplates.Remove(template);
        await SaveTemplatesAsync();
        ProgrammingTemplateCombo.SelectedIndex = _programmingTemplates.Count > 0 ? 0 : -1;
        StatusBarText.Text = $"Template “{template.Name}” deleted.";
    }

    private async Task SaveTemplatesAsync()
    {
        try
        {
            await _templateStore.SaveAsync(_programmingTemplates);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            ShowWarning($"The template could not be saved: {exception.Message}");
        }
    }

    private bool TryReadProgrammingValues(out ProgrammingTemplate template, string name)
    {
        template = ProgrammingTemplate.Defaults[0] with { Name = name };
        if (!IsFourDigitHex(SubscriberIdTextBox.Text.Trim().ToUpperInvariant()) ||
            !int.TryParse(CheckHoursTextBox.Text, out var hours) ||
            !int.TryParse(CheckMinutesTextBox.Text, out var minutes) ||
            hours is < 0 or > 24 ||
            minutes is < 0 or > 59 ||
            !int.TryParse(ReportDelayTextBox.Text, out var reportDelay) ||
            reportDelay is < 0 or > 330)
        {
            ShowWarning("Before saving a template, check the subscriber ID, check-in time, and report-delay values.");
            return false;
        }

        var zones = ZoneConfigTextBox.Text.Trim().ToUpperInvariant();
        var restorals = RestoralConfigTextBox.Text.Trim().ToUpperInvariant();
        if (zones.Length != 8 || restorals.Length != 8)
        {
            ShowWarning("A template requires exactly eight zone and eight restoral characters.");
            return false;
        }

        template = new ProgrammingTemplate(
            name,
            SelectedModel,
            SubscriberIdTextBox.Text.Trim().ToUpperInvariant(),
            hours,
            minutes,
            RequiredOrDefault(AcDelayTextBox.Text, "RM").ToUpperInvariant(),
            reportDelay,
            FireTroubleCombo.SelectedIndex == 0,
            zones,
            restorals,
            RepeatingCheckBox.IsChecked == true,
            SuppressAcCheckBox.IsChecked == true,
            SuppressChargerCheckBox.IsChecked == true,
            SuppressGroundCheckBox.IsChecked == true,
            (ProgrammingAntennaCombo.SelectedItem as AntennaOption)?.DisplayName ??
            AntennaCatalog.All[0].DisplayName);
        return true;
    }

    private async void Connect_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (_client?.IsConnected == true)
            {
                await DisconnectCurrentAsync();
                return;
            }

            IAesConnection connection;
            if (SimulationCheckBox.IsChecked == true)
            {
                connection = new SimulatedAesConnection(SelectedModel);
            }
            else
            {
                var port = PortCombo.SelectedItem as string;
                if (string.IsNullOrWhiteSpace(port))
                {
                    ShowWarning("Choose a COM port or enable Simulation.");
                    return;
                }

                connection = new SerialAesConnection(port);
            }

            _client = new AesProtocolClient(connection);
            _client.DataReceived += Client_DataReceived;
            await _client.ConnectAsync();
            AppendSystem($"CONNECTED: {_client.DisplayName}");
            StatusBarText.Text = $"Connected to {_client.DisplayName}.";
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            await DisposeClientAsync();
            MessageBox.Show(
                this,
                $"Unable to open the AES connection.\n\n{exception.Message}",
                "Connection failed",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
        finally
        {
            UpdateConnectionUi();
        }
    }

    private void RefreshPorts_Click(object sender, RoutedEventArgs e) => RefreshPorts();

    private void Navigate_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: string tag } || !int.TryParse(tag, out var index))
        {
            return;
        }

        WorkspaceTabs.SelectedIndex = index;
        PageTitle.Text = PageTitles[index];
        PageSubtitle.Text = PageSubtitles[index];
    }

    private async void Command_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: string commandName } ||
            !Enum.TryParse<AesCommand>(commandName, out var command))
        {
            return;
        }

        await SendCommandAsync(command);
        if (command is AesCommand.TimeToLive or AesCommand.SendText or AesCommand.Function1 or AesCommand.Function3)
        {
            NavigateTo(10);
            StatusBarText.Text = "Interactive function started. Read the terminal prompt and use Send line to respond.";
        }
    }

    private async void ProgramIdentity_Click(object sender, RoutedEventArgs e)
    {
        var id = SubscriberIdTextBox.Text.Trim().ToUpperInvariant();
        var cipher = CipherTextBox.Text.Trim().ToUpperInvariant();
        if (!IsFourDigitHex(id))
        {
            ShowWarning("Subscriber ID must contain exactly four hexadecimal characters (0–9, A–F).");
            return;
        }

        if (cipher.Length > 0 && !IsFourDigitHex(cipher))
        {
            ShowWarning("Cipher must be blank to preserve the existing value, or exactly four hexadecimal characters.");
            return;
        }

        if (!ConfirmProgramming("Program subscriber identity",
                $"Program ID {id} and {(cipher.Length == 0 ? "preserve the existing cipher" : "replace the system cipher")}?"))
        {
            return;
        }

        await RunConversationAsync(
            AesCommand.ProgramIdCipher,
            [id, cipher],
            "ID and cipher sequence sent. Verify the subscriber displays OK.");
    }

    private async void ProgramTimers_Click(object sender, RoutedEventArgs e)
    {
        if (!int.TryParse(CheckHoursTextBox.Text, out var hours) ||
            !int.TryParse(CheckMinutesTextBox.Text, out var minutes) ||
            hours is < 0 or > 24 ||
            minutes is < 0 or > 59 ||
            (hours == 0 && minutes == 0))
        {
            ShowWarning("Enter a valid check-in time from 00:01 through 24:00.");
            return;
        }

        var acDelay = AcDelayTextBox.Text.Trim().ToUpperInvariant();
        if (!string.Equals(acDelay, "RM", StringComparison.Ordinal) &&
            (!int.TryParse(acDelay, out var acMinutes) || acMinutes is < 0 or > 60))
        {
            ShowWarning("AC report delay must be RM or a value from 0 through 60 minutes.");
            return;
        }

        if (!int.TryParse(ReportDelayTextBox.Text, out var reportDelay) ||
            reportDelay is < 0 or > 330)
        {
            ShowWarning("Reporting delay must be from 0 through 330 seconds.");
            return;
        }

        if (reportDelay is < 10 or > 20)
        {
            var result = MessageBox.Show(
                this,
                "The legacy AES fire manuals permit a broader functional range, but identify 10–20 seconds for listed fire installations.\n\nContinue with this non-standard fire value?",
                "Reporting-delay warning",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);
            if (result != MessageBoxResult.Yes)
            {
                return;
            }
        }

        await RunConversationAsync(
            AesCommand.ProgramTimers,
            [
                hours.ToString("00", CultureInfo.InvariantCulture),
                minutes.ToString("00", CultureInfo.InvariantCulture),
                acDelay,
                reportDelay.ToString(CultureInfo.InvariantCulture)
            ],
            "Timer sequence sent. Verify the subscriber displays OK.");
    }

    private async void ProgramZones_Click(object sender, RoutedEventArgs e)
    {
        var fireTrouble = FireTroubleCombo.SelectedIndex == 0 ? "Y" : "N";
        var zones = ZoneConfigTextBox.Text.Trim().ToUpperInvariant();
        var restorals = RestoralConfigTextBox.Text.Trim().ToUpperInvariant();
        var allowedZones = fireTrouble == "Y" ? "FSB" : "SB";

        if (zones.Length != 8 || zones.Any(character => !allowedZones.Contains(character, StringComparison.Ordinal)))
        {
            ShowWarning($"Zone configuration must contain exactly eight {string.Join("/", allowedZones.ToCharArray())} characters.");
            return;
        }

        if (restorals.Length != 8 || restorals.Any(character => character is not ('R' or 'X')))
        {
            ShowWarning("Restoral configuration must contain exactly eight R/X characters.");
            return;
        }

        if (fireTrouble == "Y" && zones.All(character => character != 'F'))
        {
            var result = MessageBox.Show(
                this,
                "Fire/Trouble packets are enabled, but no zone is programmed F. Continue?",
                "Zone-programming warning",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);
            if (result != MessageBoxResult.Yes)
            {
                return;
            }
        }

        await RunConversationAsync(
            AesCommand.ProgramZones,
            [fireTrouble, zones, restorals],
            "Zone sequence sent. IMPORTANT: press the physical board RESET button, then re-read zone status.");

        MessageBox.Show(
            this,
            "The zone sequence was sent.\n\nPress the physical RESET button on the AES circuit board so the subscriber reinitializes its zone states. Do not use Reset RAM for this step.",
            "Physical board reset required",
            MessageBoxButton.OK,
            MessageBoxImage.Information);
    }

    private async void ProgramModes_Click(object sender, RoutedEventArgs e)
    {
        var responses = new List<string>
        {
            YesNo(RepeatingCheckBox.IsChecked == true),
            YesNo(SuppressAcCheckBox.IsChecked == true)
        };

        if (SelectedModel == AesModel.Aes7788F)
        {
            responses.Add(YesNo(SuppressChargerCheckBox.IsChecked == true));
            responses.Add(YesNo(SuppressGroundCheckBox.IsChecked == true));
        }

        if (responses.Skip(1).Any(value => value == "Y"))
        {
            var result = MessageBox.Show(
                this,
                "One or more trouble reports will be suppressed. The AES manuals require these suppression settings to be N for listed fire installations.\n\nContinue?",
                "Trouble-report suppression",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);
            if (result != MessageBoxResult.Yes)
            {
                return;
            }
        }

        await RunConversationAsync(
            AesCommand.ProgramModes,
            responses,
            "Operating-mode sequence sent. Verify the subscriber displays OK.");
    }

    private async void ResetRam_Click(object sender, RoutedEventArgs e)
    {
        if (!string.Equals(ResetConfirmationTextBox.Text.Trim(), "RESET RAM", StringComparison.OrdinalIgnoreCase))
        {
            ShowWarning("Type RESET RAM exactly before using this destructive function.");
            return;
        }

        var result = MessageBox.Show(
            this,
            "Reset RAM will factory-default timers, zones, restorals, and operating modes. ID and cipher remain.\n\nHave you recorded the current configuration, and do you want to continue?",
            "Confirm Reset RAM",
            MessageBoxButton.YesNo,
            MessageBoxImage.Stop);
        if (result != MessageBoxResult.Yes)
        {
            return;
        }

        await RunConversationAsync(
            AesCommand.ResetRam,
            ["Y"],
            "Reset RAM confirmation sent. Reprogram all required settings and complete a full acceptance test.");
        ResetConfirmationTextBox.Clear();
    }

    private async void MonitorToggle_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: string commandName } ||
            !Enum.TryParse<AesCommand>(commandName, out var command) ||
            !EnsureConnected())
        {
            return;
        }

        if (command == AesCommand.MonitorAll && !_monitorAllEnabled && !_receiveMonitorEnabled)
        {
            await SendCommandAsync(AesCommand.ReceiveMonitor);
            _receiveMonitorEnabled = true;
            await Task.Delay(250);
        }

        await SendCommandAsync(command);
        switch (command)
        {
            case AesCommand.ReceiveMonitor:
                _receiveMonitorEnabled = !_receiveMonitorEnabled;
                if (!_receiveMonitorEnabled)
                {
                    _monitorAllEnabled = false;
                }
                break;
            case AesCommand.TransmitMonitor:
                _transmitMonitorEnabled = !_transmitMonitorEnabled;
                break;
            case AesCommand.MonitorAll:
                _monitorAllEnabled = !_monitorAllEnabled;
                break;
        }

        UpdateMonitorButtons();
    }

    private async void StopMonitors_Click(object sender, RoutedEventArgs e)
    {
        if (!EnsureConnected())
        {
            return;
        }

        if (_monitorAllEnabled)
        {
            await SendCommandAsync(AesCommand.MonitorAll);
        }
        if (_transmitMonitorEnabled)
        {
            await SendCommandAsync(AesCommand.TransmitMonitor);
        }
        if (_receiveMonitorEnabled)
        {
            await SendCommandAsync(AesCommand.ReceiveMonitor);
        }

        _monitorAllEnabled = false;
        _transmitMonitorEnabled = false;
        _receiveMonitorEnabled = false;
        UpdateMonitorButtons();
        StatusBarText.Text = "All tracked monitor functions were toggled off.";
    }

    private async void KeyTransmitter_Click(object sender, RoutedEventArgs e)
    {
        if (RfLoadConfirmedCheckBox.IsChecked != true || AccountOnTestCheckBox.IsChecked != true)
        {
            ShowWarning("Confirm both the correct RF load/antenna and transmission authorization before keying the transmitter.");
            return;
        }

        var result = MessageBox.Show(
            this,
            "This will place the AES transmitter on the air for approximately five seconds.\n\nContinue?",
            "Confirm RF transmitter test",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);
        if (result != MessageBoxResult.Yes)
        {
            return;
        }

        await SendCommandAsync(AesCommand.KeyTransmitter);
        StatusBarText.Text = "RF transmitter key command sent. Press Abort with ENTER to stop it early.";
    }

    private async void AbortTransmitter_Click(object sender, RoutedEventArgs e)
    {
        if (!EnsureConnected() || _client is null)
        {
            return;
        }

        await _client.SendRawAsync("\r");
        AppendSent("<ENTER>");
        StatusBarText.Text = "ENTER sent to abort the transmitter test.";
    }

    private async void RunBaseline_Click(object sender, RoutedEventArgs e)
    {
        if (!EnsureConnected())
        {
            return;
        }

        try
        {
            StatusBarText.Text = "Running baseline: local status…";
            await SendCommandAsync(AesCommand.LocalStatus);
            await Task.Delay(850);
            StatusBarText.Text = "Running baseline: routing table…";
            await SendCommandAsync(AesCommand.RoutingTable);
            await Task.Delay(850);
            StatusBarText.Text = "Running baseline: zone status…";
            await SendCommandAsync(AesCommand.ZoneStatus);
            await Task.Delay(850);
            RefreshFindings();
            StatusBarText.Text = $"Baseline complete — {_findings.Count} finding(s).";
        }
        catch (Exception exception) when (exception is IOException or InvalidOperationException)
        {
            ShowWarning($"The baseline sequence stopped: {exception.Message}");
        }
    }

    private void AddSurveyTrial_Click(object sender, RoutedEventArgs e)
    {
        if (!int.TryParse(SurveyNetconTextBox.Text, out var netcon) || netcon is < 0 or > 7 ||
            !int.TryParse(SurveyRouteCountTextBox.Text, out var routeCount) || routeCount is < 0 or > 8 ||
            !TryParseDecimal(SurveyAckTextBox.Text, out var ack) || ack is < 0 or > 100 ||
            !TryParseDecimal(SurveyForwardTextBox.Text, out var forward) || forward < 0 ||
            !TryParseDecimal(SurveyReflectedTextBox.Text, out var reflected) || reflected < 0 ||
            !TryParseDecimal(SurveyIdleVoltageTextBox.Text, out var idle) || idle < 0 ||
            !TryParseDecimal(SurveyKeyedVoltageTextBox.Text, out var keyed) || keyed < 0)
        {
            ShowWarning("Check NETCON, route count, ACK percentage, power, and voltage values.");
            return;
        }

        var quality = SurveyQualityTextBox.Text.Trim().ToUpperInvariant();
        if (!Regex.IsMatch(quality, "^[0-9A-F]{2}$", RegexOptions.CultureInvariant))
        {
            ShowWarning("Best Q must be a two-character value such as 03, 82, or 81.");
            return;
        }

        var trial = new SiteSurveyTrial(
            DateTimeOffset.Now,
            RequiredOrDefault(SurveyLocationTextBox.Text, $"Trial {_surveyTrials.Count + 1}"),
            (SurveyAntennaCombo.SelectedItem as AntennaOption)?.DisplayName ?? "Not recorded",
            RequiredOrDefault(SurveyCableTextBox.Text, "Not recorded"),
            netcon,
            quality,
            routeCount,
            ack,
            forward,
            reflected,
            idle,
            keyed,
            SurveyNotesTextBox.Text.Trim());
        _surveyTrials.Add(trial);
        RefreshFindings();

        SurveyComparisonText.Text = trial.ForwardPowerWatts > 0 && trial.ReflectedPowerPercent > 10
            ? $"WARNING: {trial.Location} has {trial.ReflectedPowerPercent:0.#}% reflected power, above the 10% criterion."
            : $"Recorded {trial.Location}: NETCON {trial.NetCon}, Q{trial.BestQuality}, {trial.RouteCount} routes.";
        StatusBarText.Text = $"Survey trial “{trial.Location}” recorded.";
    }

    private void CompareTrials_Click(object sender, RoutedEventArgs e)
    {
        if (_surveyTrials.Count == 0)
        {
            ShowWarning("Add at least one survey trial.");
            return;
        }

        var best = DiagnosticEngine.SelectBestTrial(_surveyTrials);
        if (best is null)
        {
            return;
        }

        var baseline = _surveyTrials[0];
        var improvement = baseline.NetCon - best.NetCon;
        var qualityImprovement =
            DiagnosticEngine.QualityScore(best.BestQuality) - DiagnosticEngine.QualityScore(baseline.BestQuality);

        var inference = best.Location.Contains("outside", StringComparison.OrdinalIgnoreCase) &&
                        (improvement > 0 || qualityImprovement > 0)
            ? "The outside result improved, so building attenuation is a probable contributor. Evaluate a properly mounted remote antenna with the shortest practical listed 50-ohm coax."
            : "Use the best result as the next controlled reference. Change only one variable per trial before making an antenna recommendation.";

        SurveyComparisonText.Text =
            $"Best evidence: {best.Location} — NETCON {best.NetCon}, Q{best.BestQuality}, {best.RouteCount} routes, ACK {best.AckSuccessPercent:0.#}%. {inference}";
    }

    private async void TroubleMapLookup_Click(object sender, RoutedEventArgs e)
    {
        if (!TryReadCoordinates(MapLatitudeTextBox.Text, MapLongitudeTextBox.Text, out var latitude, out var longitude))
        {
            ShowWarning("Enter a valid latitude and longitude.");
            return;
        }

        try
        {
            StatusBarText.Text = "Loading Emergency24 antenna layers…";
            var analysis = await _coverageService.AnalyzeAsync(latitude, longitude);
            ApplyCoverageAnalysis(analysis);
            MapEvidenceText.Text = string.Join(Environment.NewLine, analysis.Results.Select(result => result.Summary));
            StatusBarText.Text = "Emergency24 map evidence added to the troubleshooter.";
        }
        catch (Exception exception) when (exception is HttpRequestException or IOException or JsonException)
        {
            ShowWarning($"The Emergency24 coverage layers could not be read: {exception.Message}");
        }
    }

    private async void AnalyzeRadioSite_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            RadioRecommendationText.Text = "Analyzing address and public coverage layers…";
            RadioRecommendationDetailText.Text = "This can take several seconds on the first lookup.";

            var apiKey = GoogleApiKeyPasswordBox.Password;
            var address = RadioCheckAddressTextBox.Text.Trim();
            double latitude;
            double longitude;
            _lastBuildingData = null;

            if (!string.IsNullOrWhiteSpace(apiKey) && !string.IsNullOrWhiteSpace(address))
            {
                StatusBarText.Text = "Reading Google geocoding, elevation, and building imagery insights…";
                _lastBuildingData = await _googleSiteDataService.AnalyzeAsync(address, apiKey);
                latitude = _lastBuildingData.Latitude;
                longitude = _lastBuildingData.Longitude;
                RadioCheckLatitudeTextBox.Text = latitude.ToString("0.000000", CultureInfo.InvariantCulture);
                RadioCheckLongitudeTextBox.Text = longitude.ToString("0.000000", CultureInfo.InvariantCulture);
                RadioCheckAddressTextBox.Text = _lastBuildingData.FormattedAddress;
            }
            else if (!TryReadCoordinates(
                         RadioCheckLatitudeTextBox.Text,
                         RadioCheckLongitudeTextBox.Text,
                         out latitude,
                         out longitude))
            {
                ShowWarning("Enter a Google Maps Platform API key with an address, or supply valid latitude and longitude for an AES-map-only check.");
                RadioRecommendationText.Text = "Site analysis needs an address/API key or coordinates.";
                return;
            }

            StatusBarText.Text = "Comparing all Emergency24 antenna layers…";
            var coverage = await _coverageService.AnalyzeAsync(latitude, longitude);
            ApplyCoverageAnalysis(coverage);
            RadioCoverageGrid.ItemsSource = coverage.Results;
            MapLatitudeTextBox.Text = latitude.ToString("0.000000", CultureInfo.InvariantCulture);
            MapLongitudeTextBox.Text = longitude.ToString("0.000000", CultureInfo.InvariantCulture);
            MapEvidenceText.Text = string.Join(Environment.NewLine, coverage.Results.Select(result => result.Summary));

            var recommendation = RadioRecommendationEngine.Recommend(
                coverage,
                _lastBuildingData,
                SelectedComboText(ConstructionCombo),
                SelectedComboText(PreferredLocationCombo));
            _lastRadioRecommendation = recommendation;
            RadioRecommendationText.Text = $"{recommendation.Antenna} — {recommendation.Location}";
            RadioRecommendationDetailText.Text =
                $"{recommendation.Rationale}{Environment.NewLine}{Environment.NewLine}{recommendation.Limitations}";
            BuildingDataText.Text = FormatBuildingData(_lastBuildingData, latitude, longitude);
            StatusBarText.Text = "Radio check complete. Field verification is still required.";
        }
        catch (Exception exception) when (exception is HttpRequestException or IOException or InvalidOperationException or JsonException)
        {
            RadioRecommendationText.Text = "The radio check could not be completed.";
            RadioRecommendationDetailText.Text = exception.Message;
            ShowWarning($"Site analysis failed: {exception.Message}");
        }
    }

    private void ApplyCoverageAnalysis(AesMapCoverageAnalysis analysis)
    {
        _lastCoverageAnalysis = analysis;
        _mappedCoverage = analysis.Recommended ??
                          analysis.Results
                              .Where(result => result.ExpectedNetCon.HasValue)
                              .OrderBy(result => result.ExpectedNetCon)
                              .ThenBy(result => result.GainDb)
                              .FirstOrDefault() ??
                          analysis.Results.LastOrDefault();
        RefreshFindings();
    }

    private static string FormatBuildingData(
        BuildingSiteData? building,
        double latitude,
        double longitude)
    {
        if (building is null)
        {
            return $"Coordinates: {latitude:0.000000}, {longitude:0.000000}. Google building/elevation data was not requested; recommendation uses Emergency24 coverage plus technician-selected construction.";
        }

        return string.Join(
            Environment.NewLine,
            $"Address: {building.FormattedAddress}",
            $"Coordinates: {building.Latitude:0.000000}, {building.Longitude:0.000000}",
            $"Ground elevation: {FormatMeters(building.GroundElevationMeters)} · Estimated building height: {FormatMeters(building.EstimatedBuildingHeightMeters)}",
            $"Roof area: {FormatSquareMeters(building.RoofAreaSquareMeters)} · Dominant pitch: {FormatDegrees(building.RoofPitchDegrees)} · Azimuth: {FormatDegrees(building.RoofAzimuthDegrees)}",
            $"Imagery quality: {building.ImageryQuality}. {building.Notes}");
    }

    private static string FormatMeters(double? value) =>
        value.HasValue ? $"{value.Value:0.#} m" : "not available";

    private static string FormatSquareMeters(double? value) =>
        value.HasValue ? $"{value.Value:0.#} m²" : "not available";

    private static string FormatDegrees(double? value) =>
        value.HasValue ? $"{value.Value:0.#}°" : "not available";

    private void OpenEmergencyMap_Click(object sender, RoutedEventArgs e) =>
        OpenUrl(Emergency24CoverageService.MapUrl);

    private void OpenGoogleMaps_Click(object sender, RoutedEventArgs e)
    {
        var query = GetLocationQuery();
        OpenUrl($"https://www.google.com/maps/search/?api=1&query={Uri.EscapeDataString(query)}");
    }

    private void OpenGoogleEarth_Click(object sender, RoutedEventArgs e)
    {
        var query = GetLocationQuery();
        OpenUrl($"https://earth.google.com/web/search/{Uri.EscapeDataString(query)}");
    }

    private string GetLocationQuery()
    {
        if (TryReadCoordinates(
                RadioCheckLatitudeTextBox.Text,
                RadioCheckLongitudeTextBox.Text,
                out var latitude,
                out var longitude))
        {
            return string.Create(CultureInfo.InvariantCulture, $"{latitude},{longitude}");
        }

        return RequiredOrDefault(RadioCheckAddressTextBox.Text, "Chicago, IL");
    }

    private static void OpenUrl(string url)
    {
        Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
    }

    private void AddStarterMeshRadios()
    {
        if (_virtualRadios.Count > 0)
        {
            return;
        }

        _virtualRadios.Add(new VirtualRadio
        {
            RadioId = "1001",
            Name = "Lobby",
            Model = AesModel.Aes7788F,
            NetCon = 3,
            LinkLayer = 1,
            Quality = "03"
        });
        _virtualRadios.Add(new VirtualRadio
        {
            RadioId = "2002",
            Name = "Warehouse",
            Model = AesModel.Aes7744F,
            NetCon = 5,
            LinkLayer = 2,
            Quality = "02"
        });
        _virtualRadios.Add(new VirtualRadio
        {
            RadioId = "3003",
            Name = "Roof",
            Model = AesModel.Aes7788F,
            NetCon = 4,
            LinkLayer = 1,
            Quality = "03"
        });
        RefreshMeshControls();
    }

    private void AddMeshRadio_Click(object sender, RoutedEventArgs e)
    {
        if (_virtualRadios.Count >= 4)
        {
            ShowWarning("The virtual mesh is limited to four radios.");
            return;
        }

        var radioId = MeshRadioIdTextBox.Text.Trim().ToUpperInvariant();
        if (!IsFourDigitHex(radioId) ||
            _virtualRadios.Any(radio => string.Equals(radio.RadioId, radioId, StringComparison.OrdinalIgnoreCase)) ||
            !int.TryParse(MeshNetConTextBox.Text, out var netCon) || netCon is < 0 or > 7 ||
            !int.TryParse(MeshLinkTextBox.Text, out var link) || link is < 0 or > 8)
        {
            ShowWarning("Use a unique four-character hexadecimal ID, NETCON 0–7, and link layer 0–8.");
            return;
        }

        _virtualRadios.Add(new VirtualRadio
        {
            RadioId = radioId,
            Name = RequiredOrDefault(MeshRadioNameTextBox.Text, $"Radio {_virtualRadios.Count + 1}"),
            Model = MeshModelCombo.SelectedIndex == 0 ? AesModel.Aes7744F : AesModel.Aes7788F,
            NetCon = netCon,
            LinkLayer = link,
            Quality = SelectedComboText(MeshQualityCombo),
            Online = MeshOnlineCheckBox.IsChecked == true
        });
        RefreshMeshControls();
        StatusBarText.Text = $"Simulated radio {radioId} added.";
    }

    private void RemoveMeshRadio_Click(object sender, RoutedEventArgs e)
    {
        if (VirtualRadiosGrid.SelectedItem is not VirtualRadio radio)
        {
            ShowWarning("Select a simulated radio to remove.");
            return;
        }

        _virtualRadios.Remove(radio);
        RefreshMeshControls();
        StatusBarText.Text = $"Simulated radio {radio.RadioId} removed.";
    }

    private void VirtualRadiosGrid_CellEditEnding(object sender, DataGridCellEditEndingEventArgs e) =>
        Dispatcher.BeginInvoke(new Action(RefreshMeshControls));

    private void RefreshMeshControls()
    {
        VirtualRadiosGrid.Items.Refresh();
        MeshSourceCombo.Items.Refresh();
        if (MeshSourceCombo.SelectedIndex < 0 && _virtualRadios.Count > 0)
        {
            MeshSourceCombo.SelectedIndex = 0;
        }

        var previous = MeshDestinationCombo.SelectedItem as string;
        var destinations = new[] { "BROADCAST" }
            .Concat(_virtualRadios.Select(radio => radio.RadioId))
            .ToArray();
        MeshDestinationCombo.ItemsSource = destinations;
        MeshDestinationCombo.SelectedItem = previous is not null && destinations.Contains(previous)
            ? previous
            : destinations.FirstOrDefault();
        DrawMesh();
    }

    private async void SendMeshSignal_Click(object sender, RoutedEventArgs e)
    {
        SendMeshSignal();
        await Task.CompletedTask;
    }

    private async void BurstMeshSignal_Click(object sender, RoutedEventArgs e)
    {
        for (var index = 0; index < 10; index++)
        {
            SendMeshSignal();
            await Task.Delay(45);
        }
    }

    private void SendMeshSignal()
    {
        if (_virtualRadios.Count < 2 ||
            MeshSourceCombo.SelectedItem is not VirtualRadio source ||
            MeshDestinationCombo.SelectedItem is not string destination)
        {
            ShowWarning("Add at least two radios and choose a source and destination.");
            return;
        }

        if (string.Equals(source.RadioId, destination, StringComparison.OrdinalIgnoreCase))
        {
            ShowWarning("Choose a different destination or BROADCAST.");
            return;
        }

        try
        {
            var events = _meshSimulator.Send(
                _virtualRadios.ToArray(),
                source.RadioId,
                destination,
                SelectedComboText(MeshSignalTypeCombo));
            foreach (var meshEvent in events.Reverse())
            {
                _meshEvents.Insert(0, meshEvent);
            }
            while (_meshEvents.Count > 200)
            {
                _meshEvents.RemoveAt(_meshEvents.Count - 1);
            }
            DrawMesh(events.LastOrDefault());
            StatusBarText.Text = $"Simulated {events.Count} signal path(s); no real RF was transmitted.";
        }
        catch (ArgumentException exception)
        {
            ShowWarning(exception.Message);
        }
    }

    private void DrawMesh(VirtualMeshSignal? latestSignal = null)
    {
        if (MeshCanvas is null)
        {
            return;
        }

        MeshCanvas.Children.Clear();
        var width = MeshCanvas.ActualWidth > 100 ? MeshCanvas.ActualWidth : 650;
        var height = MeshCanvas.ActualHeight > 100 ? MeshCanvas.ActualHeight : 210;
        var centerX = width / 2;
        var centerY = height / 2;
        var radiusX = Math.Max(130, width / 2 - 105);
        var radiusY = Math.Max(62, height / 2 - 48);
        var points = new Dictionary<string, Point>(StringComparer.OrdinalIgnoreCase);

        for (var index = 0; index < _virtualRadios.Count; index++)
        {
            var angle = -Math.PI / 2 + index * (2 * Math.PI / Math.Max(1, _virtualRadios.Count));
            points[_virtualRadios[index].RadioId] = new Point(
                centerX + Math.Cos(angle) * radiusX,
                centerY + Math.Sin(angle) * radiusY);
        }

        for (var left = 0; left < _virtualRadios.Count; left++)
        {
            for (var right = left + 1; right < _virtualRadios.Count; right++)
            {
                var a = points[_virtualRadios[left].RadioId];
                var b = points[_virtualRadios[right].RadioId];
                MeshCanvas.Children.Add(new Line
                {
                    X1 = a.X,
                    Y1 = a.Y,
                    X2 = b.X,
                    Y2 = b.Y,
                    Stroke = new SolidColorBrush(Color.FromRgb(55, 89, 120)),
                    StrokeThickness = 1.5,
                    StrokeDashArray = [4, 4]
                });
            }
        }

        if (latestSignal is not null)
        {
            var routeIds = latestSignal.Route.Split('→', StringSplitOptions.TrimEntries);
            for (var index = 0; index < routeIds.Length - 1; index++)
            {
                if (points.TryGetValue(routeIds[index], out var a) &&
                    points.TryGetValue(routeIds[index + 1], out var b))
                {
                    MeshCanvas.Children.Add(new Line
                    {
                        X1 = a.X,
                        Y1 = a.Y,
                        X2 = b.X,
                        Y2 = b.Y,
                        Stroke = latestSignal.Result.StartsWith("RECEIVED", StringComparison.Ordinal)
                            ? new SolidColorBrush(Color.FromRgb(46, 204, 141))
                            : new SolidColorBrush(Color.FromRgb(229, 76, 88)),
                        StrokeThickness = 4
                    });
                }
            }
        }

        foreach (var radio in _virtualRadios)
        {
            var point = points[radio.RadioId];
            var ellipse = new Ellipse
            {
                Width = 66,
                Height = 66,
                Fill = radio.Online
                    ? new SolidColorBrush(Color.FromRgb(31, 96, 142))
                    : new SolidColorBrush(Color.FromRgb(90, 102, 113)),
                Stroke = Brushes.White,
                StrokeThickness = 2
            };
            Canvas.SetLeft(ellipse, point.X - 33);
            Canvas.SetTop(ellipse, point.Y - 33);
            MeshCanvas.Children.Add(ellipse);

            var label = new TextBlock
            {
                Text = $"{radio.RadioId}\nN{radio.NetCon} L{radio.LinkLayer} Q{radio.Quality}",
                Foreground = Brushes.White,
                FontWeight = FontWeights.SemiBold,
                FontSize = 11,
                TextAlignment = TextAlignment.Center,
                Width = 100
            };
            Canvas.SetLeft(label, point.X - 50);
            Canvas.SetTop(label, point.Y - 21);
            MeshCanvas.Children.Add(label);
        }
    }

    private async void TrainingGuideCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (TrainingGuideCombo.SelectedItem is not TrainingGuide guide)
        {
            return;
        }

        var textPath = Path.Combine(AppContext.BaseDirectory, "Assets", "Training", guide.TextFile);
        TrainingDocumentTitle.Text = guide.Title;
        TrainingCoverImage.Source = new BitmapImage(
            new Uri($"pack://application:,,,/Assets/Training/{guide.CoverFile}", UriKind.Absolute));
        TrainingSearchStatusText.Text = string.Empty;
        _trainingSearchStart = 0;

        try
        {
            TrainingDocumentTextBox.Text = await File.ReadAllTextAsync(textPath, Encoding.UTF8);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            TrainingDocumentTextBox.Text = $"Unable to read the bundled guide text: {exception.Message}";
        }
    }

    private void OpenTrainingPdf_Click(object sender, RoutedEventArgs e)
    {
        if (TrainingGuideCombo.SelectedItem is not TrainingGuide guide)
        {
            return;
        }

        var path = Path.Combine(AppContext.BaseDirectory, "Assets", "Training", guide.PdfFile);
        if (!File.Exists(path))
        {
            ShowWarning("The bundled PDF could not be found.");
            return;
        }

        Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
    }

    private void FindTrainingText_Click(object sender, RoutedEventArgs e) => FindTrainingText();

    private void TrainingSearchTextBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter)
        {
            return;
        }

        e.Handled = true;
        FindTrainingText();
    }

    private void FindTrainingText()
    {
        var term = TrainingSearchTextBox.Text;
        if (string.IsNullOrWhiteSpace(term))
        {
            TrainingSearchStatusText.Text = "Enter text to find.";
            return;
        }

        var index = TrainingDocumentTextBox.Text.IndexOf(
            term,
            _trainingSearchStart,
            StringComparison.OrdinalIgnoreCase);
        if (index < 0 && _trainingSearchStart > 0)
        {
            index = TrainingDocumentTextBox.Text.IndexOf(term, StringComparison.OrdinalIgnoreCase);
        }

        if (index < 0)
        {
            TrainingSearchStatusText.Text = $"“{term}” was not found.";
            _trainingSearchStart = 0;
            return;
        }

        TrainingDocumentTextBox.Focus();
        TrainingDocumentTextBox.Select(index, term.Length);
        TrainingDocumentTextBox.ScrollToLine(
            TrainingDocumentTextBox.GetLineIndexFromCharacterIndex(index));
        _trainingSearchStart = index + term.Length;
        TrainingSearchStatusText.Text = $"Found at character {index + 1:N0}.";
    }

    private async void ExportReport_Click(object sender, RoutedEventArgs e)
    {
        RefreshFindings();
        var dialog = new SaveFileDialog
        {
            Title = "Export Superior AES field report",
            Filter = "HTML report (*.html)|*.html",
            DefaultExt = ".html",
            AddExtension = true,
            FileName = $"Superior-AES-Field-Report-{DateTime.Now:yyyyMMdd-HHmm}.html"
        };
        if (dialog.ShowDialog(this) != true)
        {
            return;
        }

        var context = new ReportContext(
            RequiredOrDefault(ReportSiteTextBox.Text, "Site not entered"),
            RequiredOrDefault(ReportAccountTextBox.Text, "Account not entered"),
            RequiredOrDefault(ReportTechnicianTextBox.Text, Environment.UserName),
            SelectedModel,
            _lastStatus,
            _routes.ToArray(),
            _zones.ToArray(),
            _findings.ToArray(),
            _surveyTrials.ToArray(),
            _transcript.ToString(),
            _lastCoverageAnalysis,
            _lastBuildingData,
            _lastRadioRecommendation);

        await File.WriteAllTextAsync(dialog.FileName, HtmlReportGenerator.Generate(context), Encoding.UTF8);
        StatusBarText.Text = $"Report saved to {dialog.FileName}.";
    }

    private async void SaveLog_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new SaveFileDialog
        {
            Title = "Save AES session log",
            Filter = "Text log (*.txt)|*.txt",
            DefaultExt = ".txt",
            AddExtension = true,
            FileName = $"Superior-AES-Session-{DateTime.Now:yyyyMMdd-HHmm}.txt"
        };
        if (dialog.ShowDialog(this) != true)
        {
            return;
        }

        await File.WriteAllTextAsync(dialog.FileName, _transcript.ToString(), Encoding.UTF8);
        StatusBarText.Text = $"Session log saved to {dialog.FileName}.";
    }

    private async void SendTerminalLine_Click(object sender, RoutedEventArgs e) => await SendTerminalLineAsync();

    private async void TerminalInput_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter)
        {
            return;
        }

        e.Handled = true;
        await SendTerminalLineAsync();
    }

    private async Task SendTerminalLineAsync()
    {
        if (!EnsureConnected() || _client is null)
        {
            return;
        }

        var value = TerminalInputTextBox.Text;
        await _client.SendLineAsync(value);
        AppendSent(value.Length == 0 ? "<ENTER>" : value);
        TerminalInputTextBox.Clear();
    }

    private async void SendEnter_Click(object sender, RoutedEventArgs e)
    {
        if (!EnsureConnected() || _client is null)
        {
            return;
        }

        await _client.SendRawAsync("\r");
        AppendSent("<ENTER>");
    }

    private void ClearTerminal_Click(object sender, RoutedEventArgs e) => TerminalOutputTextBox.Clear();

    private void ClearMonitor_Click(object sender, RoutedEventArgs e) => MonitorOutputTextBox.Clear();

    private async Task SendCommandAsync(AesCommand command)
    {
        if (!EnsureConnected() || _client is null)
        {
            return;
        }

        try
        {
            await _client.SendCommandAsync(command);
            AppendSent(AesCommands.DisplayName(command));
            StatusBarText.Text = $"{AesCommands.DisplayName(command)} sent.";
        }
        catch (Exception exception) when (exception is IOException or InvalidOperationException or TimeoutException)
        {
            ShowWarning($"Unable to send the command: {exception.Message}");
        }
    }

    private async Task RunConversationAsync(
        AesCommand command,
        IReadOnlyList<string> responses,
        string completionMessage)
    {
        if (!EnsureConnected() || _client is null)
        {
            return;
        }

        try
        {
            AppendSent(AesCommands.DisplayName(command));
            StatusBarText.Text = $"Sending guided {AesCommands.DisplayName(command)} sequence…";
            await _client.RunConversationAsync(command, responses);
            for (var index = 0; index < responses.Count; index++)
            {
                var response = responses[index];
                var display = command == AesCommand.ProgramIdCipher && index == 1 && response.Length > 0
                    ? "••••"
                    : response.Length == 0 ? "<ENTER / preserve>" : response;
                AppendSent(display);
            }
            StatusBarText.Text = completionMessage;
        }
        catch (Exception exception) when (exception is IOException or InvalidOperationException or TimeoutException)
        {
            ShowWarning($"The programming sequence stopped: {exception.Message}");
        }
    }

    private void Client_DataReceived(object? sender, AesDataReceivedEventArgs e)
    {
        Dispatcher.Invoke(() =>
        {
            AppendReceived(e.Text);
            ParseReceivedData(e.Text);
        });
    }

    private void ParseReceivedData(string text)
    {
        _parseBuffer.Append(text);
        if (_parseBuffer.Length > 50000)
        {
            _parseBuffer.Remove(0, _parseBuffer.Length - 35000);
        }

        var current = _parseBuffer.ToString();
        var status = AesParsers.ParseLocalStatus(current);
        if (status is not null)
        {
            _lastStatus = status;
            UpdateStatusUi(status);
        }

        var parsedRoutes = AesParsers.ParseRoutes(current);
        if (parsedRoutes.Count > 0)
        {
            ReplaceCollection(_routes, parsedRoutes);
            DashboardRouteValue.Text = $"{_routes.Count} route(s) · RT1 {_routes[0].Id}";
        }

        var parsedZones = AesParsers.ParseZones(current);
        if (parsedZones.Count > 0)
        {
            ReplaceCollection(_zones, parsedZones);
        }
    }

    private void UpdateStatusUi(AesLocalStatus status)
    {
        StatusModelText.Text = status.Model;
        StatusIdText.Text = status.SubscriberId;
        StatusRt1Text.Text = status.RouteOne;
        StatusLevelText.Text = status.Level.ToString(CultureInfo.InvariantCulture);
        StatusStatText.Text = status.StatCode;
        StatusNetconText.Text = status.NetCon.ToString(CultureInfo.InvariantCulture);

        StatusNetconText.Foreground = status.NetCon switch
        {
            <= 5 => (Brush)FindResource("GreenBrush"),
            6 => (Brush)FindResource("AmberBrush"),
            _ => (Brush)FindResource("RedBrush")
        };
        StatusStatText.Foreground = status.StatCode == "000"
            ? (Brush)FindResource("GreenBrush")
            : (Brush)FindResource("RedBrush");

        DashboardSubscriberValue.Text = status.SubscriberId;
        DashboardModelValue.Text = $"{status.Model} · firmware {RequiredOrDefault(status.Firmware, "not reported")}";
        DashboardNetconValue.Text = $"NETCON {status.NetCon}";
        DashboardStatValue.Text = $"STAT {status.StatCode}";
    }

    private void RefreshFindings()
    {
        ReplaceCollection(
            _findings,
            DiagnosticEngine.Analyze(
                _lastStatus,
                _routes.ToArray(),
                _surveyTrials.ToArray(),
                _mappedCoverage));
    }

    private void RefreshPorts()
    {
        var selected = PortCombo.SelectedItem as string;
        var ports = SerialAesConnection.GetAvailablePorts();
        PortCombo.ItemsSource = ports;
        PortCombo.SelectedItem = selected is not null && ports.Contains(selected, StringComparer.OrdinalIgnoreCase)
            ? selected
            : ports.FirstOrDefault();
        StatusBarText.Text = ports.Count == 0
            ? "No COM ports found. Simulation remains available."
            : $"Found {ports.Count} COM port(s).";
    }

    private void UpdateConnectionUi()
    {
        var connected = _client?.IsConnected == true;
        ConnectButton.Content = connected ? "Disconnect" : "Connect";
        ConnectionStateText.Text = connected ? "Connected" : "Not connected";
        ConnectionDetailText.Text = connected
            ? _client?.DisplayName ?? "Connected"
            : "Choose simulation or a COM port";
        ConnectionDot.Fill = connected
            ? (Brush)FindResource("GreenBrush")
            : new SolidColorBrush(Color.FromRgb(117, 134, 154));
        DashboardConnectionValue.Text = connected ? "Online" : "Offline";
        ModelCombo.IsEnabled = !connected;
        PortCombo.IsEnabled = !connected;
        SimulationCheckBox.IsEnabled = !connected;
    }

    private void UpdateMonitorButtons()
    {
        RxMonitorButton.Content = $"Receive monitor: {OnOff(_receiveMonitorEnabled)}";
        TxMonitorButton.Content = $"Transmit monitor: {OnOff(_transmitMonitorEnabled)}";
        AllMonitorButton.Content = $"Monitor all: {OnOff(_monitorAllEnabled)}";
    }

    private void AppendReceived(string text)
    {
        var stamp = DateTime.Now.ToString("HH:mm:ss.fff", CultureInfo.InvariantCulture);
        _transcript.Append('[').Append(stamp).Append(" RX] ").Append(text);
        TerminalOutputTextBox.AppendText(text);
        MonitorOutputTextBox.AppendText(text);
        TerminalOutputTextBox.ScrollToEnd();
        MonitorOutputTextBox.ScrollToEnd();
    }

    private void AppendSent(string text)
    {
        var line = $"\r\n[{DateTime.Now:HH:mm:ss.fff} TX] {text}\r\n";
        _transcript.Append(line);
        TerminalOutputTextBox.AppendText(line);
        TerminalOutputTextBox.ScrollToEnd();
    }

    private void AppendSystem(string text)
    {
        var line = $"\r\n[{DateTime.Now:HH:mm:ss.fff} APP] {text}\r\n";
        _transcript.Append(line);
        TerminalOutputTextBox.AppendText(line);
        MonitorOutputTextBox.AppendText(line);
        TerminalOutputTextBox.ScrollToEnd();
        MonitorOutputTextBox.ScrollToEnd();
    }

    private bool EnsureConnected()
    {
        if (_client?.IsConnected == true)
        {
            return true;
        }

        ShowWarning("Connect to a subscriber or enable Simulation first.");
        return false;
    }

    private void NavigateTo(int index)
    {
        WorkspaceTabs.SelectedIndex = index;
        PageTitle.Text = PageTitles[index];
        PageSubtitle.Text = PageSubtitles[index];
    }

    private async Task DisconnectCurrentAsync()
    {
        if (_client is null)
        {
            return;
        }

        AppendSystem("DISCONNECTED");
        await DisposeClientAsync();
        _receiveMonitorEnabled = false;
        _transmitMonitorEnabled = false;
        _monitorAllEnabled = false;
        UpdateMonitorButtons();
        UpdateConnectionUi();
        StatusBarText.Text = "Disconnected.";
    }

    private async Task DisposeClientAsync()
    {
        if (_client is null)
        {
            return;
        }

        _client.DataReceived -= Client_DataReceived;
        await _client.DisposeAsync();
        _client = null;
    }

    private void Window_Closing(object? sender, CancelEventArgs e)
    {
        if (_isClosing)
        {
            return;
        }

        _isClosing = true;
        if (_client is not null)
        {
            _client.DataReceived -= Client_DataReceived;
            _client.DisposeAsync().AsTask().GetAwaiter().GetResult();
            _client = null;
        }
    }

    private bool ConfirmProgramming(string title, string message) =>
        MessageBox.Show(
            this,
            $"{message}\n\nThe account should be on test and current programming should be recorded.",
            title,
            MessageBoxButton.YesNo,
            MessageBoxImage.Question) == MessageBoxResult.Yes;

    private void ShowWarning(string message) =>
        MessageBox.Show(this, message, "Superior AES Programmer", MessageBoxButton.OK, MessageBoxImage.Warning);

    private static bool IsFourDigitHex(string value) =>
        Regex.IsMatch(value, "^[0-9A-F]{4}$", RegexOptions.CultureInvariant);

    private static string YesNo(bool value) => value ? "Y" : "N";
    private static string OnOff(bool value) => value ? "ON" : "OFF";

    private static string RequiredOrDefault(string value, string fallback) =>
        string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();

    private static bool TryParseDecimal(string value, out decimal result) =>
        decimal.TryParse(value, NumberStyles.Number, CultureInfo.CurrentCulture, out result) ||
        decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out result);

    private static bool TryReadCoordinates(
        string latitudeText,
        string longitudeText,
        out double latitude,
        out double longitude)
    {
        latitude = 0;
        longitude = 0;
        var latitudeParsed =
            double.TryParse(latitudeText, NumberStyles.Float, CultureInfo.InvariantCulture, out latitude) ||
            double.TryParse(latitudeText, NumberStyles.Float, CultureInfo.CurrentCulture, out latitude);
        var longitudeParsed =
            double.TryParse(longitudeText, NumberStyles.Float, CultureInfo.InvariantCulture, out longitude) ||
            double.TryParse(longitudeText, NumberStyles.Float, CultureInfo.CurrentCulture, out longitude);
        return latitudeParsed &&
               longitudeParsed &&
               latitude is >= -90 and <= 90 &&
               longitude is >= -180 and <= 180;
    }

    private static string SelectedComboText(ComboBox comboBox) =>
        comboBox.SelectedItem is ComboBoxItem item
            ? item.Content?.ToString() ?? string.Empty
            : comboBox.SelectedItem?.ToString() ?? string.Empty;

    private static void ReplaceCollection<T>(ObservableCollection<T> target, IEnumerable<T> values)
    {
        target.Clear();
        foreach (var value in values)
        {
            target.Add(value);
        }
    }

    private sealed record TrainingGuide(
        string Title,
        string PdfFile,
        string TextFile,
        string CoverFile);
}
