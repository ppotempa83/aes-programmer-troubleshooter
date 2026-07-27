using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using SuperiorAes.Android.Models;
using SuperiorAes.Core.Diagnostics;
using SuperiorAes.Core.Models;
using SuperiorAes.Core.Protocol;
using SuperiorAes.Core.Reporting;

namespace SuperiorAes.Android.Services;

public sealed partial class CompanionSession : ICompanionSession
{
    private readonly IAesTransport _transport;
    private readonly IAesTransportSelector _transportSelector;
    private readonly List<SessionLogEntry> _entries = [];
    private readonly List<RouteEntry> _routes = [];
    private readonly List<ZoneState> _zones = [];
    private readonly List<SiteSurveyTrial> _surveyTrials = [];
    private readonly HashSet<string> _sensitiveValues = new(StringComparer.Ordinal);
    private readonly SemaphoreSlim _operationGate = new(1, 1);
    private readonly StringBuilder _parseBuffer = new();
    private readonly object _gate = new();
    private AesMapCoverageAnalysis? _latestCoverage;
    private BuildingSiteData? _latestBuilding;
    private RadioSiteRecommendation? _latestRadioRecommendation;
    private int _rawIdentityStep;

    public CompanionSession(IAesTransportSelector transport)
    {
        _transport = transport;
        _transportSelector = transport;
        _transport.DataReceived += OnDataReceived;
        _transport.ConnectionStateChanged += OnConnectionStateChanged;
        SessionId = Guid.NewGuid();
        SessionStarted = DateTimeOffset.Now;
        Append(
            "SYSTEM",
            $"SESSION STARTED · {SessionId:D} · device {DeviceDescription()} · user {Environment.UserName}");
    }

    public event EventHandler? StateChanged;

    public Guid SessionId { get; }
    public DateTimeOffset SessionStarted { get; }
    public string SelectedModel { get; private set; } = "7788F";
    public bool IsConnected => _transport.IsConnected;
    public string ConnectionName => _transport.DisplayName;
    public AesTransportMode TransportMode => _transportSelector.SelectedMode;
    public bool IsBusy { get; private set; }
    public string BusyMessage { get; private set; } = string.Empty;
    public string SubscriberId { get; private set; } = string.Empty;
    public string TechnicianName { get; private set; } = string.Empty;
    public AesLocalStatus? LastStatus { get; private set; }

    public AesMapCoverageAnalysis? LatestCoverage
    {
        get
        {
            lock (_gate)
            {
                return _latestCoverage;
            }
        }
    }

    public BuildingSiteData? LatestBuilding
    {
        get
        {
            lock (_gate)
            {
                return _latestBuilding;
            }
        }
    }

    public RadioSiteRecommendation? LatestRadioRecommendation
    {
        get
        {
            lock (_gate)
            {
                return _latestRadioRecommendation;
            }
        }
    }

    public IReadOnlyList<RouteEntry> Routes
    {
        get
        {
            lock (_gate)
            {
                return _routes.ToArray();
            }
        }
    }

    public IReadOnlyList<ZoneState> Zones
    {
        get
        {
            lock (_gate)
            {
                return _zones.ToArray();
            }
        }
    }

    public IReadOnlyList<SiteSurveyTrial> SurveyTrials
    {
        get
        {
            lock (_gate)
            {
                return _surveyTrials.ToArray();
            }
        }
    }

    public IReadOnlyList<SessionLogEntry> Entries
    {
        get
        {
            lock (_gate)
            {
                return _entries.ToArray();
            }
        }
    }

    public string Transcript => string.Join(
        Environment.NewLine,
        Entries.Select(entry => entry.Formatted));

    public void SelectModel(string model)
    {
        if (model is not ("7744F" or "7788F"))
        {
            throw new ArgumentOutOfRangeException(nameof(model), model, "Only 7744F and 7788F are supported.");
        }

        SelectedModel = model;
        Append("ACTIVITY", $"Selected model {model}");
    }

    public void SetTechnicianName(string value)
    {
        TechnicianName = SafeMetadata(value, 80);
        Append("ACTIVITY", "Technician/session label updated");
    }

    public void SetSubscriberId(string value)
    {
        var normalized = value.Trim().ToUpperInvariant();
        if (!FourHexInputRegex().IsMatch(normalized))
        {
            throw new ArgumentException("Subscriber ID must be four hexadecimal characters.", nameof(value));
        }

        SubscriberId = normalized;
        Append("ACTIVITY", $"Subscriber ID set to {normalized}");
    }

    public void AddSurveyTrial(SiteSurveyTrial trial)
    {
        lock (_gate)
        {
            _surveyTrials.Add(trial);
        }

        Append(
            "ACTIVITY",
            $"Survey trial recorded · {SafeMetadata(trial.Location, 80)} · NETCON {trial.NetCon} · Q{trial.BestQuality}");
    }

    public void RecordCoverageAnalysis(AesMapCoverageAnalysis coverage)
    {
        ArgumentNullException.ThrowIfNull(coverage);
        lock (_gate)
        {
            var samePoint = _latestCoverage is not null &&
                            Math.Abs(_latestCoverage.Latitude - coverage.Latitude) < 0.000001 &&
                            Math.Abs(_latestCoverage.Longitude - coverage.Longitude) < 0.000001;
            _latestCoverage = coverage;
            if (!samePoint)
            {
                _latestBuilding = null;
                _latestRadioRecommendation = null;
            }
        }

        Append(
            "ACTIVITY",
            $"Emergency24 coverage retained · {coverage.Latitude:0.000000}, {coverage.Longitude:0.000000}");
    }

    public void RecordSiteAnalysis(
        AesMapCoverageAnalysis coverage,
        BuildingSiteData? building,
        RadioSiteRecommendation? recommendation)
    {
        ArgumentNullException.ThrowIfNull(coverage);
        lock (_gate)
        {
            _latestCoverage = coverage;
            _latestBuilding = building;
            _latestRadioRecommendation = recommendation;
        }

        Append(
            "ACTIVITY",
            $"Site analysis retained · {coverage.Latitude:0.000000}, {coverage.Longitude:0.000000} · " +
            $"Geoapify building evidence {(building is null ? "not used" : "included")}");
    }

    public void RecordActivity(string action) => Append("ACTIVITY", action);

    public void RegisterSensitiveValue(string value)
    {
        var normalized = value.Trim();
        if (normalized.Length < 4)
        {
            return;
        }

        lock (_gate)
        {
            _sensitiveValues.Add(normalized);
        }
    }

    public async Task SelectTransportModeAsync(
        AesTransportMode mode,
        bool hardwareSafetyWarningAccepted,
        CancellationToken cancellationToken = default)
    {
        await _operationGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        SetBusy(true, $"Selecting {mode}");
        try
        {
            await _transportSelector.SelectModeAsync(
                    mode,
                    hardwareSafetyWarningAccepted,
                    cancellationToken)
                .ConfigureAwait(false);
            Append("ACTIVITY", $"Transport mode selected · {mode}");
        }
        finally
        {
            SetBusy(false, string.Empty);
            _operationGate.Release();
        }
    }

    public async Task ConnectAsync(CancellationToken cancellationToken = default)
    {
        await _operationGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        SetBusy(true, "Connecting transport");
        try
        {
            Append("ACTIVITY", $"Connect requested · {_transport.DisplayName}");
            await _transport.ConnectAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            SetBusy(false, string.Empty);
            _operationGate.Release();
        }
    }

    public async Task DisconnectAsync(CancellationToken cancellationToken = default)
    {
        await _operationGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        SetBusy(true, "Disconnecting transport");
        try
        {
            Append("ACTIVITY", "Disconnect requested");
            await _transport.DisconnectAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            SetBusy(false, string.Empty);
            _operationGate.Release();
        }
    }

    public async Task SendCommandAsync(
        AesCommand command,
        CancellationToken cancellationToken = default)
    {
        await _operationGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        SetBusy(true, $"Sending {AesCommands.DisplayName(command)}");
        try
        {
            EnsureConnected();
            await SendCommandCoreAsync(command, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            SetBusy(false, string.Empty);
            _operationGate.Release();
        }
    }

    public async Task RunConversationAsync(
        AesCommand command,
        IReadOnlyList<GuidedResponse> responses,
        CancellationToken cancellationToken = default)
    {
        await _operationGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        SetBusy(true, $"Programming · {AesCommands.DisplayName(command)}");
        try
        {
            EnsureConnected();
            await SendCommandCoreAsync(command, cancellationToken).ConfigureAwait(false);
            for (var index = 0; index < responses.Count; index++)
            {
                var response = responses[index];
                if (response.IsSensitive)
                {
                    RegisterSensitiveValue(response.Value);
                }

                await Task.Delay(TimeSpan.FromMilliseconds(650), cancellationToken)
                    .ConfigureAwait(false);
                await _transport.SendAsync(
                        Encoding.ASCII.GetBytes(response.Value + "\r"),
                        cancellationToken)
                    .ConfigureAwait(false);
                Append(
                    "TX",
                    response.IsSensitive
                        ? "[REDACTED CIPHER]"
                        : string.IsNullOrEmpty(response.DisplayValue) ? "<ENTER>" : response.DisplayValue);
            }
        }
        finally
        {
            SetBusy(false, string.Empty);
            _operationGate.Release();
        }
    }

    public Task RunGuidedActionAsync(
        string action,
        string result,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Append("ACTION", action);
        AppendMultiline("RESULT", result);
        return Task.CompletedTask;
    }

    public async Task SendRawAsync(string value, CancellationToken cancellationToken = default)
    {
        if (value is null)
        {
            return;
        }

        await _operationGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        SetBusy(true, "Sending terminal line");
        try
        {
            EnsureConnected();
            var trimmed = value.Trim();
            var display = value.Length == 0 ? "<ENTER>" : value;

            if (_rawIdentityStep == 2)
            {
                RegisterSensitiveValue(trimmed);
                display = string.IsNullOrEmpty(value) ? "<ENTER / preserve cipher>" : "[REDACTED CIPHER]";
                _rawIdentityStep = 0;
            }
            else if (_rawIdentityStep == 1)
            {
                if (FourHexInputRegex().IsMatch(trimmed))
                {
                    SubscriberId = trimmed.ToUpperInvariant();
                }

                _rawIdentityStep = 2;
            }
            else if (string.Equals(trimmed, "f", StringComparison.OrdinalIgnoreCase))
            {
                _rawIdentityStep = 1;
            }
            else if (FourHexInputRegex().IsMatch(trimmed))
            {
                display = "[REDACTED FOUR-HEX INPUT]";
            }

            Append("TX", display);
            await _transport.SendAsync(Encoding.ASCII.GetBytes(value + "\r"), cancellationToken)
                .ConfigureAwait(false);
        }
        finally
        {
            SetBusy(false, string.Empty);
            _operationGate.Release();
        }
    }

    public async Task SendEnterAsync(CancellationToken cancellationToken = default)
    {
        await _operationGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        SetBusy(true, "Sending ENTER");
        try
        {
            EnsureConnected();
            Append("TX", "<ENTER>");
            await _transport.SendAsync("\r"u8.ToArray(), cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            SetBusy(false, string.Empty);
            _operationGate.Release();
        }
    }

    public async Task SendEscapeAsync(CancellationToken cancellationToken = default)
    {
        await _operationGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        SetBusy(true, "Sending ESC");
        try
        {
            EnsureConnected();
            Append("TX", "<ESC>");
            await _transport.SendAsync(new byte[] { 0x1B }, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            SetBusy(false, string.Empty);
            _operationGate.Release();
        }
    }

    public async Task<SessionExportResult> ExportSessionAsync(
        CancellationToken cancellationToken = default)
    {
        Append("ACTIVITY", "Session text and spreadsheet export requested");
        var directory = Path.Combine(FileSystem.Current.AppDataDirectory, "Exports");
        Directory.CreateDirectory(directory);
        var stem =
            $"Superior-AES-Session-{DateTimeOffset.Now.LocalDateTime:yyyyMMdd-HHmmss}-{SessionId:N}";
        var textPath = Path.Combine(directory, $"{stem}.txt");
        var spreadsheetPath = Path.Combine(directory, $"{stem}.xlsx");
        var context = CreateSessionExportContext();

        await File.WriteAllTextAsync(
            textPath,
            SessionExportService.BuildText(context),
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
            cancellationToken);
        await SessionExportService.WriteSpreadsheetAsync(
            spreadsheetPath,
            context,
            cancellationToken);
        Append("SYSTEM", $"Session exports created · {Path.GetFileName(textPath)} and {Path.GetFileName(spreadsheetPath)}");
        return new SessionExportResult(textPath, spreadsheetPath);
    }

    public async Task<string> ExportTroubleshootingAsync(
        string siteName,
        string accountNumber,
        string technician,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Append("ACTIVITY", "Troubleshooting-only export requested");
        var coverage = LatestCoverage;
        var findings = DiagnosticEngine.Analyze(
            LastStatus,
            Routes,
            SurveyTrials,
            SelectMappedCoverage(coverage));
        var context = new TroubleshootingReportContext(
            SafeMetadata(siteName, 120, "Site not entered"),
            SafeMetadata(accountNumber, 80, "Account not entered"),
            SafeMetadata(technician, 80, TechnicianName.Length == 0 ? Environment.UserName : TechnicianName),
            SelectedModel == "7744F" ? AesModel.Aes7744F : AesModel.Aes7788F,
            LastStatus,
            Routes,
            findings,
            coverage);
        var directory = Path.Combine(FileSystem.Current.AppDataDirectory, "Exports");
        Directory.CreateDirectory(directory);
        var path = Path.Combine(
            directory,
            $"Superior-AES-Troubleshooting-{DateTimeOffset.Now.LocalDateTime:yyyyMMdd-HHmmss}.html");
        await File.WriteAllTextAsync(
            path,
            TroubleshootingReportGenerator.Generate(context),
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
            cancellationToken);
        Append("SYSTEM", $"Troubleshooting-only export created · {Path.GetFileName(path)}");
        return path;
    }

    private void OnDataReceived(object? sender, AesTransportDataReceivedEventArgs args)
    {
        var safe = Redact(args.Text);
        lock (_gate)
        {
            _parseBuffer.Append(safe);
            if (_parseBuffer.Length > 50_000)
            {
                _parseBuffer.Remove(0, _parseBuffer.Length - 35_000);
            }

            var current = _parseBuffer.ToString();
            var status = AesParsers.ParseLocalStatus(current);
            if (status is not null)
            {
                LastStatus = status;
                SubscriberId = status.SubscriberId;
                SelectedModel = status.Model;
            }

            var routes = AesParsers.ParseRoutes(safe);
            if (routes.Count == 0)
            {
                routes = AesParsers.ParseRoutes(current);
            }
            if (routes.Count > 0)
            {
                _routes.Clear();
                _routes.AddRange(routes);
            }

            var zones = AesParsers.ParseZones(current);
            if (zones.Count > 0)
            {
                _zones.Clear();
                _zones.AddRange(zones);
            }
        }

        AppendMultiline("RX", safe);
    }

    private void OnConnectionStateChanged(object? sender, EventArgs args) =>
        Append("SYSTEM", IsConnected ? $"CONNECTED · {ConnectionName}" : "DISCONNECTED");

    private void AppendMultiline(string channel, string message)
    {
        var safeText = Redact(message)
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n');
        var lines = safeText.Split('\n');
        var appended = false;
        foreach (var line in lines)
        {
            if (line.Length == 0 && lines.Length > 1)
            {
                continue;
            }

            Append(channel, line);
            appended = true;
        }

        if (!appended)
        {
            Append(channel, "<EMPTY>");
        }
    }

    private void Append(string channel, string message)
    {
        var safeMessage = Redact(message)
            .Replace("\r", " ", StringComparison.Ordinal)
            .Replace("\n", " ", StringComparison.Ordinal);
        lock (_gate)
        {
            _entries.Add(new SessionLogEntry(DateTimeOffset.Now, channel, safeMessage));
        }

        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    private string Redact(string value)
    {
        var sessionId = SessionId.ToString("D", CultureInfo.InvariantCulture);
        var redacted = value.Replace(sessionId, "{SESSION-ID}", StringComparison.OrdinalIgnoreCase);
        string[] secrets;
        lock (_gate)
        {
            secrets = _sensitiveValues.ToArray();
        }

        foreach (var secret in secrets)
        {
            redacted = redacted.Replace(secret, "[REDACTED SENSITIVE VALUE]", StringComparison.Ordinal);
        }

        redacted = SecretAssignmentRegex().Replace(redacted, "$1=[REDACTED]");
        redacted = LongTokenRegex().Replace(redacted, "[REDACTED TOKEN]");
        return redacted.Replace("{SESSION-ID}", sessionId, StringComparison.Ordinal);
    }

    private SessionExportContext CreateSessionExportContext()
    {
        var user = string.IsNullOrWhiteSpace(TechnicianName)
            ? Environment.UserName
            : $"{Environment.UserName} · technician {TechnicianName}";
        return new SessionExportContext(
            SessionStarted,
            DateTimeOffset.Now,
            SubscriberId,
            LastStatus?.Model ?? SelectedModel,
            DeviceDescription(),
            user,
            IsConnected ? ConnectionName : $"Disconnected · last transport {ConnectionName}",
            Transcript,
            SessionId);
    }

    private static AesMapCoverageResult? SelectMappedCoverage(
        AesMapCoverageAnalysis? coverage) =>
        coverage?.Recommended ??
        coverage?.Results
            .Where(result => result.ExpectedNetCon.HasValue)
            .OrderBy(result => result.ExpectedNetCon)
            .ThenBy(result => result.GainDb)
            .FirstOrDefault() ??
        coverage?.Results.LastOrDefault();

    private void EnsureConnected()
    {
        if (!IsConnected)
        {
            throw new InvalidOperationException("Connect to the selected AES transport before sending a command.");
        }
    }

    private async Task SendCommandCoreAsync(
        AesCommand command,
        CancellationToken cancellationToken)
    {
        var displayName = AesCommands.DisplayName(command);
        Append("TX", displayName);
        await _transport.SendAsync(AesCommands.GetBytes(command), cancellationToken)
            .ConfigureAwait(false);
    }

    private void SetBusy(bool isBusy, string message)
    {
        IsBusy = isBusy;
        BusyMessage = message;
        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    private static string DeviceDescription()
    {
        var info = DeviceInfo.Current;
        return $"{info.Manufacturer} {info.Model} · {info.Name} · {info.Platform} {info.VersionString}";
    }

    private static string SafeMetadata(string value, int maxLength, string fallback = "")
    {
        var normalized = value.Trim()
            .Replace("\r", " ", StringComparison.Ordinal)
            .Replace("\n", " ", StringComparison.Ordinal);
        if (normalized.Length == 0)
        {
            return fallback;
        }

        return normalized.Length <= maxLength ? normalized : normalized[..maxLength];
    }

    [GeneratedRegex(
        @"(?i)\b(api[-_ ]?key|access[-_ ]?token|password|secret|cipher)\b\s*[:=]\s*\S+",
        RegexOptions.CultureInvariant)]
    private static partial Regex SecretAssignmentRegex();

    [GeneratedRegex(@"\b[0-9A-Za-z_-]{24,}\b", RegexOptions.CultureInvariant)]
    private static partial Regex LongTokenRegex();

    [GeneratedRegex(@"^[0-9A-Fa-f]{4}$", RegexOptions.CultureInvariant)]
    private static partial Regex FourHexInputRegex();
}
