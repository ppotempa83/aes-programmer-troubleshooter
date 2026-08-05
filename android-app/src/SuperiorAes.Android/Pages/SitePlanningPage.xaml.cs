using System.Globalization;
using System.Text.Json;
using SuperiorAes.Android.Services;
using SuperiorAes.Core.Diagnostics;
using SuperiorAes.Core.Models;
using SuperiorAes.Core.SiteAnalysis;

namespace SuperiorAes.Android.Pages;

public partial class SitePlanningPage : ContentPage
{
    private const string GeoapifyStorageKey = "GeoapifyApiKey";
    private readonly ICompanionSession _session;
    private readonly GeoapifySiteDataService _geoapify = new();
    private readonly Emergency24CoverageService _coverage = new();

    public SitePlanningPage(ICompanionSession session)
    {
        InitializeComponent();
        _session = session;
        SurveyAntennaPicker.ItemsSource = AntennaCatalog.All.Select(value => value.DisplayName).ToArray();
        SurveyAntennaPicker.SelectedIndex = 0;
        ConstructionPicker.ItemsSource = new[]
        {
            "Wood / light-frame",
            "Brick / masonry",
            "Concrete",
            "Metal building",
            "High-rise / reinforced concrete",
            "Mixed / verify on site"
        };
        PreferredLocationPicker.ItemsSource = new[]
        {
            "Automatic — evidence based",
            "Highest practical interior test point",
            "Exterior wall",
            "Roof / above obstructions",
            "Existing listed antenna location"
        };
        ConstructionPicker.SelectedIndex = 0;
        PreferredLocationPicker.SelectedIndex = 0;
        RefreshSurveyTrials();
        ApplyLatestSiteState();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        RefreshSurveyTrials();
        ApplyLatestSiteState();
        var stored = await SecureStorage.Default.GetAsync(GeoapifyStorageKey);
        if (!string.IsNullOrWhiteSpace(stored))
        {
            _session.RegisterSensitiveValue(stored);
            GeoapifyKeyEntry.Text = stored;
        }

        var supplied = await DisplayPromptAsync(
            "Geoapify setup",
            $"{(string.IsNullOrWhiteSpace(stored) ? "No stored key was found." : "A key is already present in Android Secure Storage; leave this blank to keep using it.")} Obtain your own key at myprojects.geoapify.com, then paste it for site/new-radio tools. The value is never logged or exported.",
            "Use key",
            "Continue without key",
            "Geoapify API key",
            -1,
            Keyboard.Text,
            string.Empty);
        if (!string.IsNullOrWhiteSpace(supplied))
        {
            await StoreApiKeyAsync(supplied);
        }
        else
        {
            _session.RecordActivity(
                string.IsNullOrWhiteSpace(stored)
                    ? "Geoapify key prompt completed without supplying a key"
                    : "Geoapify key prompt completed using the securely stored value");
        }
    }

    private async void OnSaveKeyClicked(object? sender, EventArgs args)
    {
        var key = GeoapifyKeyEntry.Text?.Trim() ?? string.Empty;
        if (key.Length == 0)
        {
            await DisplayAlertAsync("Geoapify key", "Paste a key first.", "OK");
            return;
        }

        await StoreApiKeyAsync(key);
        await DisplayAlertAsync("Geoapify key ready", "The key is stored with Android Secure Storage and excluded from logs/exports.", "OK");
    }

    private async Task StoreApiKeyAsync(string key)
    {
        _session.RegisterSensitiveValue(key);
        await SecureStorage.Default.SetAsync(GeoapifyStorageKey, key);
        GeoapifyKeyEntry.Text = key;
        _session.RecordActivity("Geoapify runtime key supplied and redacted");
    }

    private async void OnGetGeoapifyKeyClicked(object? sender, EventArgs args)
    {
        await Launcher.Default.OpenAsync("https://myprojects.geoapify.com/");
        _session.RecordActivity("Official Geoapify MyProjects page opened");
    }

    private void OnSurveyClicked(object? sender, EventArgs args)
    {
        if (!int.TryParse(SurveyNetconEntry.Text, NumberStyles.None, CultureInfo.InvariantCulture, out var netcon) ||
            netcon is < 0 or > 7 ||
            !int.TryParse(SurveyRoutesEntry.Text, NumberStyles.None, CultureInfo.InvariantCulture, out var routes) ||
            routes is < 0 or > 8 ||
            !TryDecimal(SurveyAckEntry.Text, out var ack) ||
            ack is < 0 or > 100 ||
            !TryDecimal(SurveyForwardEntry.Text, out var forward) ||
            !TryDecimal(SurveyReflectedEntry.Text, out var reflected) ||
            !TryDecimal(SurveyIdleVoltageEntry.Text, out var idleVoltage) ||
            !TryDecimal(SurveyKeyedVoltageEntry.Text, out var keyedVoltage))
        {
            SurveyOutputLabel.Text = "Enter valid NETCON 0–7, routes 0–8, ACK 0–100, power, and voltage values.";
            return;
        }

        var quality = (SurveyQualityEntry.Text ?? string.Empty).Trim().ToUpperInvariant();
        if (quality is not ("03" or "02" or "01" or "83" or "82" or "81"))
        {
            SurveyOutputLabel.Text = "Q must be one of 03, 02, 01, 83, 82, or 81.";
            return;
        }

        var trial = new SiteSurveyTrial(
            DateTimeOffset.Now,
            Value(SurveyLocationEntry.Text, "Unnamed location"),
            SurveyAntennaPicker.SelectedItem?.ToString() ?? AntennaCatalog.All[0].DisplayName,
            Value(SurveyCableEntry.Text, "Not entered"),
            netcon,
            quality,
            routes,
            ack,
            forward,
            reflected,
            idleVoltage,
            keyedVoltage,
            Value(SurveyNotesEditor.Text, string.Empty));
        _session.AddSurveyTrial(trial);
        SurveyOutputLabel.Text =
            $"Recorded {trial.Location}: NETCON {trial.NetCon}, Q{trial.BestQuality}, {trial.RouteCount} routes, " +
            $"ACK {trial.AckSuccessPercent:0.#}%, reflected {trial.ReflectedPowerPercent:0.#}%, voltage drop {trial.VoltageDrop:0.00} V.";
        RefreshSurveyTrials();
    }

    private void OnCompareClicked(object? sender, EventArgs args)
    {
        var best = DiagnosticEngine.SelectBestTrial(_session.SurveyTrials);
        SurveyOutputLabel.Text = best is null
            ? "Record at least one measured trial."
            : $"Best evidence: {best.Location} · {best.Antenna} · NETCON {best.NetCon} · Q{best.BestQuality} · " +
              $"{best.RouteCount} route(s) · ACK {best.AckSuccessPercent:0.#}% · reflected {best.ReflectedPowerPercent:0.#}%.";
        RefreshSurveyTrials();
        _session.RecordActivity("Site-survey trials compared");
    }

    private async void OnRadioCheckClicked(object? sender, EventArgs args)
    {
        var address = AddressEntry.Text?.Trim() ?? string.Empty;
        var key = GeoapifyKeyEntry.Text?.Trim() ?? string.Empty;
        var coordinateTextEntered =
            !string.IsNullOrWhiteSpace(LatitudeEntry.Text) ||
            !string.IsNullOrWhiteSpace(LongitudeEntry.Text);
        var hasCoordinates = TryReadCoordinates(
            LatitudeEntry.Text,
            LongitudeEntry.Text,
            out var latitude,
            out var longitude);
        if (coordinateTextEntered && !hasCoordinates)
        {
            await DisplayAlertAsync(
                "Invalid coordinates",
                "Enter both latitude and longitude. Latitude must be -90 through 90 and longitude -180 through 180.",
                "OK");
            return;
        }

        var useGeoapify = address.Length > 0 && key.Length > 0;
        if (!useGeoapify && !hasCoordinates)
        {
            await DisplayAlertAsync(
                "Site inputs required",
                "Enter an address with a Geoapify key, or supply valid latitude and longitude for an AES-map-only analysis.",
                "OK");
            return;
        }

        BuildingSiteData? building = null;
        PlanningOutputLabel.Text = useGeoapify
            ? "Reading Geoapify address/terrain evidence and Emergency24 AES layers…"
            : "Reading Emergency24 AES antenna layers from the supplied coordinates…";
        try
        {
            if (useGeoapify)
            {
                _session.RegisterSensitiveValue(key);
                building = await _geoapify.AnalyzeAsync(address, key);
                latitude = building.Latitude;
                longitude = building.Longitude;
                AddressEntry.Text = building.FormattedAddress;
                LatitudeEntry.Text = latitude.ToString("0.000000", CultureInfo.InvariantCulture);
                LongitudeEntry.Text = longitude.ToString("0.000000", CultureInfo.InvariantCulture);
                var mapBytes = await _geoapify.GetStaticMapAsync(latitude, longitude, key);
                GeoapifyMapImage.Source = ImageSource.FromStream(
                    () => new MemoryStream(mapBytes, writable: false));
            }
            else
            {
                GeoapifyMapImage.Source = null;
            }

            var coverage = await _coverage.AnalyzeAsync(latitude, longitude);
            var recommendation = RadioRecommendationEngine.Recommend(
                coverage,
                building,
                ConstructionPicker.SelectedItem?.ToString() ?? "Verify on site",
                PreferredLocationPicker.SelectedItem?.ToString() ?? "Automatic — evidence based");
            _session.RecordSiteAnalysis(coverage, building, recommendation);
            var bestTrial = DiagnosticEngine.SelectBestTrial(_session.SurveyTrials);
            var layers = string.Join(
                Environment.NewLine,
                coverage.Results.Select(result =>
                    $"{result.Antenna} ({result.GainDb:0.#} dB): " +
                    $"{(result.ExpectedNetCon.HasValue ? $"expected N{result.ExpectedNetCon}" : "no mapped signal")} · " +
                    $"N5 peers {result.NetConFivePeers} · N6/7 peers {result.NetConSixOrSevenPeers}"));
            PlanningOutputLabel.Text =
                (building is null
                    ? $"AES-only coordinates {latitude:0.000000}, {longitude:0.000000}\n" +
                      "Geoapify address/elevation data was not requested.\n\n"
                    : $"{building.FormattedAddress}\n" +
                      $"Coordinates {building.Latitude:0.000000}, {building.Longitude:0.000000}\n" +
                      $"Ground elevation {Meters(building.GroundElevationMeters)}\n" +
                      $"{building.Notes}\n\n") +
                $"EMERGENCY24 PUBLIC AES LAYERS\n{layers}\n\n" +
                $"RECOMMENDATION\nAntenna: {recommendation.Antenna}\nLocation: {recommendation.Location}\n" +
                $"{recommendation.Rationale}\n{recommendation.Limitations}\n\n" +
                (bestTrial is null
                    ? "Recommendation: perform measured route/Q/ACK trials at code-compliant candidate antenna locations."
                    : $"Measured reference: {bestTrial.Location}, {bestTrial.Antenna}, NETCON {bestTrial.NetCon}, Q{bestTrial.BestQuality}. " +
                      "Field-compare roofline and code-compliant alternatives before selection.");
            _session.RecordActivity(
                useGeoapify
                    ? "Geoapify and Emergency24 site analysis completed"
                    : "Emergency24 coordinate-only site analysis completed");
        }
        catch (Exception exception) when (
            exception is HttpRequestException or
                IOException or
                InvalidOperationException or
                JsonException or
                TaskCanceledException)
        {
            PlanningOutputLabel.Text = $"Site analysis failed: {exception.Message}";
            _session.RecordActivity("Site analysis failed; any supplied credential remained redacted");
        }
    }

    private async void OnOpenGeoapifyClicked(object? sender, EventArgs args)
    {
        string url;
        if (TryGetCurrentCoordinates(out var latitude, out var longitude))
        {
            url = string.Create(
                CultureInfo.InvariantCulture,
                $"https://www.openstreetmap.org/?mlat={latitude}&mlon={longitude}#map=17/{latitude}/{longitude}");
        }
        else
        {
            var address = AddressEntry.Text?.Trim() ?? string.Empty;
            if (address.Length == 0)
            {
                await DisplayAlertAsync(
                    "Location required",
                    "Enter an address or coordinates before opening the map.",
                    "OK");
                return;
            }

            url =
                $"https://www.geoapify.com/tools/geocoding-online/?text={Uri.EscapeDataString(address)}";
        }

        await Launcher.Default.OpenAsync(new Uri(url));
        _session.RecordActivity("Geoapify/location map opened");
    }

    private async void OnOpenAesMapClicked(object? sender, EventArgs args)
    {
        await Launcher.Default.OpenAsync(new Uri(Emergency24CoverageService.MapUrl));
        _session.RecordActivity("Emergency24 AES map opened");
    }

    private void RefreshSurveyTrials()
    {
        var trials = _session.SurveyTrials;
        SurveyTrialsLabel.Text = trials.Count == 0
            ? "No measured trials recorded."
            : string.Join(
                Environment.NewLine,
                trials.Select((trial, index) =>
                    $"{index + 1}. {trial.Location} · {trial.Antenna} · N{trial.NetCon}/Q{trial.BestQuality} · " +
                    $"{trial.RouteCount} routes · ACK {trial.AckSuccessPercent:0.#}%"));
    }

    private void ApplyLatestSiteState()
    {
        if (_session.LatestCoverage is not { } coverage)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(LatitudeEntry.Text))
        {
            LatitudeEntry.Text = coverage.Latitude.ToString("0.000000", CultureInfo.InvariantCulture);
        }
        if (string.IsNullOrWhiteSpace(LongitudeEntry.Text))
        {
            LongitudeEntry.Text = coverage.Longitude.ToString("0.000000", CultureInfo.InvariantCulture);
        }
        if (_session.LatestBuilding is { } building &&
            string.IsNullOrWhiteSpace(AddressEntry.Text))
        {
            AddressEntry.Text = building.FormattedAddress;
        }
    }

    private bool TryGetCurrentCoordinates(out double latitude, out double longitude)
    {
        if (TryReadCoordinates(
                LatitudeEntry.Text,
                LongitudeEntry.Text,
                out latitude,
                out longitude))
        {
            return true;
        }

        if (_session.LatestCoverage is { } coverage)
        {
            latitude = coverage.Latitude;
            longitude = coverage.Longitude;
            return true;
        }

        latitude = 0;
        longitude = 0;
        return false;
    }

    private static bool TryDecimal(string? value, out decimal result) =>
        decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out result);

    private static bool TryReadCoordinates(
        string? latitudeText,
        string? longitudeText,
        out double latitude,
        out double longitude)
    {
        latitude = 0;
        longitude = 0;
        if (!TryDouble(latitudeText, out var parsedLatitude) ||
            !TryDouble(longitudeText, out var parsedLongitude))
        {
            return false;
        }

        latitude = parsedLatitude;
        longitude = parsedLongitude;
        return latitude is >= -90 and <= 90 &&
               longitude is >= -180 and <= 180;
    }

    private static bool TryDouble(string? value, out double result) =>
        double.TryParse(
            value,
            NumberStyles.Float,
            CultureInfo.InvariantCulture,
            out result) ||
        double.TryParse(
            value,
            NumberStyles.Float,
            CultureInfo.CurrentCulture,
            out result);

    private static string Value(string? value, string fallback)
    {
        var normalized = value?.Trim().Replace("\r", " ").Replace("\n", " ");
        return string.IsNullOrWhiteSpace(normalized) ? fallback : normalized;
    }

    private static string Meters(double? value) =>
        value.HasValue ? $"{value.Value:0.#} m" : "not available";
}
