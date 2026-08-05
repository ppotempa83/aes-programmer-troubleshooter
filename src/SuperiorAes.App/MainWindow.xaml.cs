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
using System.Windows.Controls.Primitives;
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
        "Raw terminal",
        "Contact ID / dialer capture"
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
        "Direct ASCII access for advanced and firmware-specific operations.",
        "Configure and test 7794 IntelliPro Fire or document a legacy 7067 IntelliTap II installation."
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
    private readonly GeoapifySiteDataService _geoapifySiteDataService = new();
    private readonly VirtualMeshSimulator _meshSimulator = new();
    private readonly ProgrammingTemplateStore _templateStore;
    private readonly DateTimeOffset _sessionStarted = DateTimeOffset.Now;
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
    private int _rawIdentityStep;

    private static readonly IReadOnlyList<TrainingGuide> TrainingGuides =
    [
        new(
            "Contact ID — IntelliPro + IntelliTap Field Guide",
            "AES-Contact-ID-IntelliPro-IntelliTap-Field-Guide.pdf",
            "AES-Contact-ID-IntelliPro-IntelliTap-Field-Guide.txt",
            "contact-id-guide-cover.png"),
        new(
            "AES 7794 IntelliPro Fire — Original Installation Manual",
            "AES-7794-IntelliPro-Fire-Installation-Manual.pdf",
            "AES-7794-IntelliPro-Fire-Installation-Manual.txt",
            "intellipro-7794-manual-cover.png"),
        new(
            "AES 7794 IntelliPro Fire — Original Quick Start",
            "AES-7794-IntelliPro-Quick-Start-Guide.pdf",
            "AES-7794-IntelliPro-Quick-Start-Guide.txt",
            "intellipro-7794-quick-start-cover.png"),
        new(
            "AES 7067 IntelliTap II — Historical Original Manual",
            "AES-7067-IntelliTap-II-Historical-Manual.pdf",
            "AES-7067-IntelliTap-II-Historical-Manual.txt",
            "intellitap-7067-manual-cover.png"),
        new(
            "AES 7794A IntelliPro 2.0 — Original Manual",
            "AES-7794A-IntelliPro-2.0-Installation-Manual.pdf",
            "AES-7794A-IntelliPro-2.0-Installation-Manual.txt",
            "intellipro-7794a-manual-cover.png"),
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
            "AES Programmer and Troubleshooter",
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
        FullCommandItemsControl.ItemsSource = AesCommands.Guides;
        AddHandler(Button.ClickEvent, new RoutedEventHandler(LogButtonActivity), true);
        AddHandler(TextBox.LostFocusEvent, new RoutedEventHandler(LogFieldActivity), true);
        AddHandler(Selector.SelectionChangedEvent, new SelectionChangedEventHandler(LogSelectionActivity), true);
        AddHandler(ToggleButton.CheckedEvent, new RoutedEventHandler(LogToggleActivity), true);
        AddHandler(ToggleButton.UncheckedEvent, new RoutedEventHandler(LogToggleActivity), true);
        MeshCanvas.SizeChanged += (_, _) => DrawMesh();
        Loaded += MainWindow_Loaded;
        RefreshPorts();
        UpdateConnectionUi();
    }

    private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        AppendSystem($"SESSION STARTED · computer {Environment.MachineName} · user {Environment.UserName}");
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
        TemplateNotesTextBox.Text = template.Notes;
        SelectComboByText(ProgrammingDialerCaptureCombo, template.DialerCaptureModule, 0);
        ContactIdModuleCombo.SelectedIndex = ProgrammingDialerCaptureCombo.SelectedIndex;
        SelectComboByText(ContactIdReportFormatCombo, template.ContactIdReportFormat, 0);
        ContactIdInterceptTextBox.Text = template.ContactIdInterceptNumber;
        SelectComboByText(ContactIdPhoneLineCombo, template.ContactIdPhoneLineMode, 0);
        SelectComboByText(
            ContactIdInputGainCombo,
            template.ContactIdInputGain.ToString(CultureInfo.InvariantCulture),
            0);
        SelectComboByText(ContactIdFourXxCombo, template.ContactIdFourXxLetter, 0);
        ContactIdTtlHoursTextBox.Text = template.ContactIdTtlHours.ToString(CultureInfo.InvariantCulture);
        ContactIdTtlMinutesTextBox.Text = template.ContactIdTtlMinutes.ToString(CultureInfo.InvariantCulture);
        ContactIdBlindDialCombo.SelectedIndex = template.ContactIdBlindDialEnabled ? 1 : 0;
        UpdateContactIdModuleStatus();
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

        var intercept = ContactIdInterceptTextBox.Text.Trim();
        if (intercept.Length is < 3 or > 20 ||
            !intercept.All(char.IsDigit) ||
            !int.TryParse(SelectedComboText(ContactIdInputGainCombo), out var inputGain) ||
            inputGain is not (10 or 20) ||
            !int.TryParse(ContactIdTtlHoursTextBox.Text, out var ttlHours) ||
            ttlHours is < 0 or > 24 ||
            !int.TryParse(ContactIdTtlMinutesTextBox.Text, out var ttlMinutes) ||
            ttlMinutes is < 0 or > 59)
        {
            ShowWarning("Check the Contact ID intercept number, input gain, and IntelliTap TTL values before saving the template.");
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
            AntennaCatalog.All[0].DisplayName,
            TemplateNotesTextBox.Text.Trim(),
            SelectedComboText(ProgrammingDialerCaptureCombo),
            SelectedComboText(ContactIdReportFormatCombo),
            intercept,
            SelectedComboText(ContactIdPhoneLineCombo),
            inputGain,
            SelectedComboText(ContactIdFourXxCombo),
            ttlHours,
            ttlMinutes,
            ContactIdBlindDialCombo.SelectedIndex == 1);
        return true;
    }

    private async void ExportTemplates_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new SaveFileDialog
        {
            Title = "Export configurable programming templates",
            Filter = "JSON templates (*.json)|*.json",
            DefaultExt = ".json",
            AddExtension = true,
            FileName = "Superior-AES-Programming-Templates.json"
        };
        if (dialog.ShowDialog(this) != true)
        {
            return;
        }

        var exportStore = new ProgrammingTemplateStore(dialog.FileName);
        await exportStore.SaveAsync(_programmingTemplates);
        AppendSystem($"TEMPLATES EXPORTED · {Path.GetFileName(dialog.FileName)}");
    }

    private async void ImportTemplates_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "Import configurable programming templates",
            Filter = "JSON templates (*.json)|*.json"
        };
        if (dialog.ShowDialog(this) != true)
        {
            return;
        }

        try
        {
            var importStore = new ProgrammingTemplateStore(dialog.FileName);
            var imported = await importStore.LoadAsync();
            ReplaceCollection(_programmingTemplates, imported);
            await SaveTemplatesAsync();
            ProgrammingTemplateCombo.SelectedIndex = _programmingTemplates.Count > 0 ? 0 : -1;
            AppendSystem($"TEMPLATES IMPORTED · {imported.Count} template(s)");
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException)
        {
            ShowWarning($"Templates could not be imported: {exception.Message}");
        }
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

        if (index < 0 || index >= PageTitles.Length)
        {
            return;
        }

        if (index is 5 or 6)
        {
            PromptForGeoapifyKey();
        }

        if (index == 11)
        {
            ContactIdModuleCombo.SelectedIndex = Math.Max(0, ProgrammingDialerCaptureCombo.SelectedIndex);
            UpdateContactIdModuleStatus();
        }

        WorkspaceTabs.SelectedIndex = index;
        PageTitle.Text = PageTitles[index];
        PageSubtitle.Text = PageSubtitles[index];
    }

    private void PromptForGeoapifyKey()
    {
        var keyBox = new PasswordBox
        {
            MinHeight = 34,
            Padding = new Thickness(9, 6, 9, 6),
            Password = GeoapifyApiKeyPasswordBox.Password
        };
        var obtainButton = new Button
        {
            Content = "Open Geoapify MyProjects",
            Style = (Style)FindResource("SecondaryButton"),
            MinWidth = 190,
            HorizontalAlignment = HorizontalAlignment.Left,
            Margin = new Thickness(0, 10, 0, 0)
        };
        obtainButton.Click += (_, _) => OpenExternalUrl("https://myprojects.geoapify.com/");
        var saveButton = new Button { Content = "Use for this session", MinWidth = 150 };
        var skipButton = new Button
        {
            Content = "Continue without key",
            Style = (Style)FindResource("SecondaryButton"),
            MinWidth = 155,
            Margin = new Thickness(8, 0, 0, 0)
        };
        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0, 16, 0, 0)
        };
        buttons.Children.Add(saveButton);
        buttons.Children.Add(skipButton);

        var panel = new StackPanel { Margin = new Thickness(22) };
        panel.Children.Add(new TextBlock
        {
            Text = "Geoapify API key",
            FontSize = 21,
            FontWeight = FontWeights.SemiBold,
            Foreground = (Brush)FindResource("NavyBrush")
        });
        panel.Children.Add(new TextBlock
        {
            Text = "Create your own Geoapify account and project, open API Keys, then copy the generated key. Paste it below; it remains only in memory for this app session and is excluded from terminal logs and exports.",
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 8, 0, 14)
        });
        panel.Children.Add(keyBox);
        panel.Children.Add(obtainButton);
        panel.Children.Add(buttons);

        var dialog = new Window
        {
            Title = "Geoapify setup",
            Owner = this,
            Width = 520,
            Height = 410,
            MinWidth = 480,
            ResizeMode = ResizeMode.NoResize,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Icon = new BitmapImage(new Uri("pack://application:,,,/Assets/Branding/superior-aes.ico", UriKind.Absolute)),
            Content = panel
        };
        saveButton.Click += (_, _) =>
        {
            if (string.IsNullOrWhiteSpace(keyBox.Password))
            {
                ShowWarning("Paste a Geoapify API key or choose Continue without key.");
                return;
            }

            GeoapifyApiKeyPasswordBox.Password = keyBox.Password;
            dialog.DialogResult = true;
        };
        skipButton.Click += (_, _) => dialog.DialogResult = false;
        dialog.Loaded += (_, _) => keyBox.Focus();
        dialog.ShowDialog();

        AppendSystem(string.IsNullOrWhiteSpace(GeoapifyApiKeyPasswordBox.Password)
            ? "GEOAPIFY KEY PROMPT · continued without a key"
            : "GEOAPIFY KEY PROMPT · runtime key supplied and redacted");
    }

    private void ProgrammingDialerCaptureCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ContactIdModuleCombo is null || ProgrammingDialerCaptureCombo.SelectedIndex < 0)
        {
            return;
        }

        if (ContactIdModuleCombo.SelectedIndex != ProgrammingDialerCaptureCombo.SelectedIndex)
        {
            ContactIdModuleCombo.SelectedIndex = ProgrammingDialerCaptureCombo.SelectedIndex;
        }

        UpdateContactIdModuleStatus();
    }

    private void ContactIdModuleCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ProgrammingDialerCaptureCombo is not null &&
            ContactIdModuleCombo.SelectedIndex >= 0 &&
            ProgrammingDialerCaptureCombo.SelectedIndex != ContactIdModuleCombo.SelectedIndex)
        {
            ProgrammingDialerCaptureCombo.SelectedIndex = ContactIdModuleCombo.SelectedIndex;
        }

        UpdateContactIdModuleStatus();
    }

    private void ContactIdCopyToProgramming_Click(object sender, RoutedEventArgs e)
    {
        ProgrammingDialerCaptureCombo.SelectedIndex = Math.Max(0, ContactIdModuleCombo.SelectedIndex);
        StatusBarText.Text = "Contact ID module and worksheet values are included the next time this programming template is saved.";
        NavigateTo(1);
    }

    private void UpdateContactIdModuleStatus()
    {
        if (ContactIdModuleStatusText is null || ContactIdModuleCombo is null)
        {
            return;
        }

        ContactIdModuleStatusText.Text = ContactIdModuleCombo.SelectedIndex switch
        {
            1 => "Recommended for 7744F / 7788F legacy fire. The 7794 replaces the discontinued 7067.",
            2 => "Historical 7744F / 7788F option only. AES lists the 7067 as discontinued and unsupported; verify the exact unit and approval before service.",
            3 => "7794A is for 7707 / 7177 IntelliNet 2.0 Fire only. Do not install it in a legacy 7744F or 7788F.",
            _ => "No dialer-capture module is selected. Contact ID programming controls remain locked."
        };
    }

    private async void ContactIdControl_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: string control } ||
            !ValidateContactIdProgrammingConnection())
        {
            return;
        }

        var explanation = control switch
        {
            "F1" => "Purpose: enter the 7794 CONFIG menu.\nEntry format: no typed input.\nExample: send F1, then read the first live option.",
            "F3" => "Purpose: change the displayed 7794 option.\nEntry format: no typed input.\nExample: with AP report format displayed, use F3 until Contact ID (C) is selected.",
            "F4" => "Purpose: move up through 7794 configuration options.\nEntry format: no typed input.\nExample: move from intercept number to the preceding option.",
            "F5" => "Purpose: move down through 7794 configuration options.\nEntry format: no typed input.\nExample: advance from report format to intercept number.",
            "ESC" => "Purpose: exit the active 7794 configuration menu.\nEntry format: no typed input.\nExample: exit after verifying the final displayed value.",
            _ => string.Empty
        };
        if (explanation.Length == 0)
        {
            return;
        }

        if (MessageBox.Show(
                this,
                $"{explanation}\n\nThe account must be on test. Send {control} to the verified 7794 J2 connection?",
                $"7794 control · {control}",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question) != MessageBoxResult.Yes ||
            _client is null)
        {
            return;
        }

        try
        {
            switch (control)
            {
                case "F1":
                    await _client.SendCommandAsync(AesCommand.Function1);
                    break;
                case "F3":
                    await _client.SendCommandAsync(AesCommand.Function3);
                    break;
                case "F4":
                    await _client.SendCommandAsync(AesCommand.RoutingTable);
                    break;
                case "F5":
                    await _client.SendCommandAsync(AesCommand.SendText);
                    break;
                case "ESC":
                    await _client.SendRawAsync("\u001b");
                    break;
            }

            AppendSent($"7794 {control}");
            StatusBarText.Text = $"7794 {control} sent through the verified J2 workflow.";
        }
        catch (Exception exception) when (exception is IOException or InvalidOperationException or TimeoutException)
        {
            ShowWarning($"Unable to send the 7794 control: {exception.Message}");
        }
    }

    private async void SendContactIdLine_Click(object sender, RoutedEventArgs e) =>
        await SendContactIdLineAsync();

    private async void ContactIdTerminalInput_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter)
        {
            return;
        }

        e.Handled = true;
        await SendContactIdLineAsync();
    }

    private async Task SendContactIdLineAsync()
    {
        if (!ValidateContactIdProgrammingConnection() || _client is null)
        {
            return;
        }

        var response = ContactIdTerminalInputTextBox.Text;
        try
        {
            await _client.SendLineAsync(response);
            AppendSent(response.Length == 0 ? "7794 <ENTER>" : $"7794 RESPONSE · {response}");
            ContactIdTerminalInputTextBox.Clear();
        }
        catch (Exception exception) when (exception is IOException or InvalidOperationException or TimeoutException)
        {
            ShowWarning($"Unable to send the 7794 response: {exception.Message}");
        }
    }

    private bool ValidateContactIdProgrammingConnection()
    {
        if (ContactIdModuleCombo.SelectedIndex != 1)
        {
            ShowWarning("Select 7794 IntelliPro Fire to use the interactive controls. IntelliTap jumper settings are historical and are not automated.");
            return false;
        }

        if (ContactIdJ2VerifiedCheckBox.IsChecked != true)
        {
            ShowWarning("Verify and check the 7794 J2 HandHeld connection safety confirmation first.");
            return false;
        }

        return EnsureConnected();
    }

    private void OpenBundledTraining_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: string requestedFile })
        {
            return;
        }

        var fileName = Path.GetFileName(requestedFile);
        var path = Path.Combine(AppContext.BaseDirectory, "Assets", "Training", fileName);
        if (!File.Exists(path))
        {
            ShowWarning("The bundled training document could not be found.");
            return;
        }

        Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
    }

    private void OpenContactIdTraining_Click(object sender, RoutedEventArgs e)
    {
        var guide = TrainingGuides.FirstOrDefault(item =>
            item.Title.StartsWith("Contact ID", StringComparison.OrdinalIgnoreCase));
        if (guide is not null)
        {
            TrainingGuideCombo.SelectedItem = guide;
        }

        NavigateTo(8);
    }

    private void OpenOfficialPage_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: string url })
        {
            OpenExternalUrl(url);
        }
    }

    private void HardwareImage_Click(object sender, MouseButtonEventArgs e)
    {
        if (sender is not System.Windows.Controls.Image { Tag: string metadata })
        {
            return;
        }

        var parts = metadata.Split('|', 3, StringSplitOptions.TrimEntries);
        if (parts.Length != 3)
        {
            return;
        }

        var productImage = new System.Windows.Controls.Image
        {
            Source = new BitmapImage(new Uri($"pack://application:,,,{parts[0]}", UriKind.Absolute)),
            Stretch = Stretch.Uniform,
            Margin = new Thickness(24)
        };
        var title = new TextBlock
        {
            Text = parts[1],
            FontSize = 24,
            FontWeight = FontWeights.SemiBold,
            Foreground = (Brush)FindResource("NavyBrush"),
            Margin = new Thickness(22, 18, 22, 0)
        };
        var linkText = new TextBox
        {
            Text = parts[2],
            IsReadOnly = true,
            FontFamily = new FontFamily("Consolas"),
            Margin = new Thickness(22, 0, 10, 18)
        };
        var openButton = new Button
        {
            Content = "Open official AES product page",
            Margin = new Thickness(0, 0, 22, 18)
        };
        openButton.Click += (_, _) => OpenExternalUrl(parts[2]);

        var footer = new Grid();
        footer.ColumnDefinitions.Add(new ColumnDefinition());
        footer.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        footer.Children.Add(linkText);
        Grid.SetColumn(openButton, 1);
        footer.Children.Add(openButton);

        var panel = new Grid { Background = Brushes.White };
        panel.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        panel.RowDefinitions.Add(new RowDefinition());
        panel.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        panel.Children.Add(title);
        Grid.SetRow(productImage, 1);
        panel.Children.Add(productImage);
        Grid.SetRow(footer, 2);
        panel.Children.Add(footer);

        var dialog = new Window
        {
            Title = parts[1],
            Owner = this,
            Width = 900,
            Height = 760,
            MinWidth = 650,
            MinHeight = 560,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Icon = new BitmapImage(new Uri("pack://application:,,,/Assets/Branding/superior-aes.ico", UriKind.Absolute)),
            Content = panel
        };
        dialog.ShowDialog();
        e.Handled = true;
    }

    private static void OpenExternalUrl(string url)
    {
        if (Uri.TryCreate(url, UriKind.Absolute, out var uri) &&
            uri.Scheme is "https" or "http")
        {
            Process.Start(new ProcessStartInfo(uri.AbsoluteUri) { UseShellExecute = true });
        }
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

    private async void GuidedCommand_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: AesCommand command })
        {
            return;
        }

        var guide = AesCommands.Guides.First(value => value.Command == command);
        var message =
            $"{guide.Explanation}\n\nENTRY FORMAT\n{guide.EntryFormat}\n\nEXAMPLE\n{guide.Example}";

        if (command is AesCommand.ProgramIdCipher or AesCommand.ProgramTimers or
            AesCommand.ProgramZones or AesCommand.ProgramModes or AesCommand.ResetRam)
        {
            MessageBox.Show(this, message, guide.Title, MessageBoxButton.OK, MessageBoxImage.Information);
            NavigateTo(1);
            StatusBarText.Text = "Enter the values in the guided panel, then use its programming button.";
            return;
        }

        if (command == AesCommand.KeyTransmitter)
        {
            MessageBox.Show(this, message, guide.Title, MessageBoxButton.OK, MessageBoxImage.Warning);
            NavigateTo(3);
            StatusBarText.Text = "Use the red transmitter-test button after completing its safety checks.";
            return;
        }

        if (MessageBox.Show(
                this,
                $"{message}\n\nSend this command now?",
                guide.Title,
                MessageBoxButton.YesNo,
                MessageBoxImage.Question) != MessageBoxResult.Yes)
        {
            return;
        }

        await SendCommandAsync(command);
        if (command is AesCommand.TimeToLive or AesCommand.SendText or AesCommand.Function1 or AesCommand.Function3)
        {
            NavigateTo(10);
            StatusBarText.Text = "Read the live prompt, enter the requested value, and select Send line.";
        }
    }

    private void LogButtonActivity(object sender, RoutedEventArgs e)
    {
        if (e.Source is not Button button)
        {
            return;
        }

        var label = button.Content?.ToString();
        if (!string.IsNullOrWhiteSpace(label))
        {
            AppendSystem($"UI ACTION · {label}");
        }
    }

    private void LogFieldActivity(object sender, RoutedEventArgs e)
    {
        if (e.Source is TextBox textBox)
        {
            AppendSystem($"UI FIELD EDITED · {ControlName(textBox)}");
        }
    }

    private void LogSelectionActivity(object sender, SelectionChangedEventArgs e)
    {
        if (e.Source is Selector selector)
        {
            AppendSystem($"UI SELECTION CHANGED · {ControlName(selector)}");
        }
    }

    private void LogToggleActivity(object sender, RoutedEventArgs e)
    {
        if (e.Source is ToggleButton toggle)
        {
            AppendSystem($"UI OPTION · {ControlName(toggle)} · {(toggle.IsChecked == true ? "selected" : "cleared")}");
        }
    }

    private static string ControlName(FrameworkElement control) =>
        string.IsNullOrWhiteSpace(control.Name) ? control.GetType().Name : control.Name;

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

    private async void ExportTroubleshooting_Click(object sender, RoutedEventArgs e)
    {
        RefreshFindings();
        if (_routes.Count == 0 &&
            MessageBox.Show(
                this,
                "No routing-table entries are currently captured. Export the report with a missing-data warning anyway?",
                "Routing data not captured",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning) != MessageBoxResult.Yes)
        {
            return;
        }

        var dialog = new SaveFileDialog
        {
            Title = "Export AES troubleshooting report",
            Filter = "HTML troubleshooting report (*.html)|*.html",
            DefaultExt = ".html",
            AddExtension = true,
            FileName = $"Superior-AES-Troubleshooting-{DateTime.Now:yyyyMMdd-HHmm}.html"
        };
        if (dialog.ShowDialog(this) != true)
        {
            return;
        }

        var context = new TroubleshootingReportContext(
            RequiredOrDefault(ReportSiteTextBox.Text, "Site not entered"),
            RequiredOrDefault(ReportAccountTextBox.Text, "Account not entered"),
            RequiredOrDefault(ReportTechnicianTextBox.Text, Environment.UserName),
            SelectedModel,
            _lastStatus,
            _routes.ToArray(),
            _findings.ToArray(),
            _lastCoverageAnalysis);
        await File.WriteAllTextAsync(
            dialog.FileName,
            TroubleshootingReportGenerator.Generate(context),
            Encoding.UTF8);
        AppendSystem($"TROUBLESHOOTING REPORT EXPORTED · {Path.GetFileName(dialog.FileName)}");
        StatusBarText.Text = $"Troubleshooting-only report saved to {dialog.FileName}.";
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

            var apiKey = GeoapifyApiKeyPasswordBox.Password;
            var address = RadioCheckAddressTextBox.Text.Trim();
            double latitude;
            double longitude;
            _lastBuildingData = null;

            if (!string.IsNullOrWhiteSpace(apiKey) && !string.IsNullOrWhiteSpace(address))
            {
                StatusBarText.Text = "Reading Geoapify geocoding and terrain elevation…";
                _lastBuildingData = await _geoapifySiteDataService.AnalyzeAsync(address, apiKey);
                latitude = _lastBuildingData.Latitude;
                longitude = _lastBuildingData.Longitude;
                RadioCheckLatitudeTextBox.Text = latitude.ToString("0.000000", CultureInfo.InvariantCulture);
                RadioCheckLongitudeTextBox.Text = longitude.ToString("0.000000", CultureInfo.InvariantCulture);
                RadioCheckAddressTextBox.Text = _lastBuildingData.FormattedAddress;
                var mapBytes = await _geoapifySiteDataService.GetStaticMapAsync(latitude, longitude, apiKey);
                using var mapStream = new MemoryStream(mapBytes);
                var mapImage = new BitmapImage();
                mapImage.BeginInit();
                mapImage.CacheOption = BitmapCacheOption.OnLoad;
                mapImage.StreamSource = mapStream;
                mapImage.EndInit();
                mapImage.Freeze();
                GeoapifyMapImage.Source = mapImage;
            }
            else if (!TryReadCoordinates(
                         RadioCheckLatitudeTextBox.Text,
                         RadioCheckLongitudeTextBox.Text,
                         out latitude,
                         out longitude))
            {
                ShowWarning("Enter a Geoapify API key with an address, or supply valid latitude and longitude for an AES-map-only check.");
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
            return $"Coordinates: {latitude:0.000000}, {longitude:0.000000}. Geoapify address/elevation data was not requested; recommendation uses Emergency24 coverage plus technician-selected construction.";
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

    private void OpenGeoapifyMap_Click(object sender, RoutedEventArgs e)
    {
        if (TryReadCoordinates(
                RadioCheckLatitudeTextBox.Text,
                RadioCheckLongitudeTextBox.Text,
                out var latitude,
                out var longitude))
        {
            OpenUrl(string.Create(
                CultureInfo.InvariantCulture,
                $"https://www.openstreetmap.org/?mlat={latitude}&mlon={longitude}#map=17/{latitude}/{longitude}"));
            return;
        }

        OpenUrl($"https://www.geoapify.com/tools/geocoding-online/?text={Uri.EscapeDataString(GetLocationQuery())}");
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

        var textPath = dialog.FileName;
        var spreadsheetPath = Path.ChangeExtension(textPath, ".xlsx");
        AppendSystem($"SESSION EXPORT REQUESTED · {Path.GetFileName(textPath)} and {Path.GetFileName(spreadsheetPath)}");
        var context = CreateSessionExportContext();
        await File.WriteAllTextAsync(textPath, SessionExportService.BuildText(context), Encoding.UTF8);
        await SessionExportService.WriteSpreadsheetAsync(spreadsheetPath, context);
        StatusBarText.Text = $"Session text and spreadsheet saved beside each other in {Path.GetDirectoryName(textPath)}.";
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
        var display = value.Length == 0 ? "<ENTER>" : value;
        if (_rawIdentityStep == 2)
        {
            display = value.Length == 0 ? "<ENTER / preserve cipher>" : "[REDACTED CIPHER]";
            _rawIdentityStep = 0;
        }
        else if (_rawIdentityStep == 1)
        {
            _rawIdentityStep = 2;
        }
        else if (string.Equals(value.Trim(), "f", StringComparison.OrdinalIgnoreCase))
        {
            _rawIdentityStep = 1;
        }
        AppendSent(display);
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
        var stamped = StampTerminalText("RX", text);
        _transcript.Append(stamped);
        TerminalOutputTextBox.AppendText(stamped);
        MonitorOutputTextBox.AppendText(stamped);
        ContactIdTerminalPreview.AppendText(stamped);
        TerminalOutputTextBox.ScrollToEnd();
        MonitorOutputTextBox.ScrollToEnd();
        ContactIdTerminalPreview.ScrollToEnd();
    }

    private void AppendSent(string text)
    {
        var line = StampTerminalText("TX", text);
        _transcript.Append(line);
        TerminalOutputTextBox.AppendText(line);
        ContactIdTerminalPreview.AppendText(line);
        TerminalOutputTextBox.ScrollToEnd();
        ContactIdTerminalPreview.ScrollToEnd();
    }

    private void AppendSystem(string text)
    {
        var line = StampTerminalText("APP", text);
        _transcript.Append(line);
        TerminalOutputTextBox.AppendText(line);
        MonitorOutputTextBox.AppendText(line);
        TerminalOutputTextBox.ScrollToEnd();
        MonitorOutputTextBox.ScrollToEnd();
    }

    private string StampTerminalText(string channel, string text)
    {
        var safeText = RedactSensitiveText(text)
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n');
        var lines = safeText.Split('\n');
        var builder = new StringBuilder();
        foreach (var line in lines)
        {
            if (line.Length == 0 && lines.Length > 1)
            {
                continue;
            }

            builder.Append(DateTime.Now.ToString(
                    "[MM-dd-yyyy / hh:mm (tt)]",
                    CultureInfo.InvariantCulture))
                .Append(' ')
                .Append(channel)
                .Append(" · ")
                .AppendLine(line);
        }

        return builder.ToString();
    }

    private string RedactSensitiveText(string value)
    {
        var redacted = value;
        var apiKey = GeoapifyApiKeyPasswordBox.Password;
        if (!string.IsNullOrWhiteSpace(apiKey))
        {
            redacted = redacted.Replace(apiKey, "[REDACTED API KEY]", StringComparison.Ordinal);
        }

        var cipher = CipherTextBox.Text.Trim();
        if (cipher.Length == 4)
        {
            redacted = redacted.Replace(cipher, "[REDACTED CIPHER]", StringComparison.OrdinalIgnoreCase);
        }

        return redacted;
    }

    private SessionExportContext CreateSessionExportContext() =>
        new(
            _sessionStarted,
            DateTimeOffset.Now,
            _lastStatus?.SubscriberId ?? SubscriberIdTextBox.Text.Trim().ToUpperInvariant(),
            _lastStatus?.Model ?? SelectedModel.ToString(),
            Environment.MachineName,
            Environment.UserName,
            _client?.DisplayName ?? "Not connected",
            _transcript.ToString());

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
        MessageBox.Show(this, message, "AES Programmer & Troubleshooter", MessageBoxButton.OK, MessageBoxImage.Warning);

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

    private static void SelectComboByText(ComboBox comboBox, string value, int fallbackIndex)
    {
        var match = comboBox.Items
            .OfType<object>()
            .FirstOrDefault(item =>
                string.Equals(
                    item is ComboBoxItem comboItem ? comboItem.Content?.ToString() : item.ToString(),
                    value,
                    StringComparison.OrdinalIgnoreCase));
        comboBox.SelectedItem = match;
        if (comboBox.SelectedIndex < 0)
        {
            comboBox.SelectedIndex = fallbackIndex;
        }
    }

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
