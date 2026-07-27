using Microsoft.Maui.Controls.Shapes;
using SuperiorAes.Android.Services;
using SuperiorAes.Core.Models;

namespace SuperiorAes.Android.Pages;

public partial class TrainingPage : ContentPage
{
    private static readonly IReadOnlyList<TrainingGuide> Guides =
    [
        new(
            "Contact ID — IntelliPro + IntelliTap Field Guide",
            "Superior-AES-Contact-ID-IntelliPro-IntelliTap-Field-Guide.pdf",
            "Superior-AES-Contact-ID-IntelliPro-IntelliTap-Field-Guide.txt"),
        new(
            "AES 7794 IntelliPro Fire — Original Installation Manual",
            "AES-7794-IntelliPro-Fire-Installation-Manual.pdf",
            "AES-7794-IntelliPro-Fire-Installation-Manual.txt"),
        new(
            "AES 7794 IntelliPro Fire — Original Quick Start",
            "AES-7794-IntelliPro-Quick-Start-Guide.pdf",
            "AES-7794-IntelliPro-Quick-Start-Guide.txt"),
        new(
            "AES 7067 IntelliTap II — Historical Original Manual",
            "AES-7067-IntelliTap-II-Historical-Manual.pdf",
            "AES-7067-IntelliTap-II-Historical-Manual.txt"),
        new(
            "AES 7794A IntelliPro 2.0 — Original Manual",
            "AES-7794A-IntelliPro-2.0-Installation-Manual.pdf",
            "AES-7794A-IntelliPro-2.0-Installation-Manual.txt"),
        new(
            "Complete Technician Guide",
            "AES-7744F-7788F-Complete-Technician-Guide.pdf",
            "AES-7744F-7788F-Complete-Technician-Guide.txt"),
        new(
            "NETCON, Signal Survey & Antenna Guide",
            "AES-7744F-7788F-NETCON-Signal-Survey-and-Antenna-Guide.pdf",
            "AES-7744F-7788F-NETCON-Signal-Survey-and-Antenna-Guide.txt"),
        new(
            "US232R Wiring & Commands",
            "AES-7744F-7788F-US232R-Wiring-and-Commands.pdf",
            "AES-7744F-7788F-US232R-Wiring-and-Commands.txt")
    ];

    private static readonly IReadOnlyList<HardwareAsset> Hardware =
    [
        new("AES 7744F / 7788F Fire Subscriber", "aes-7744f-7788f.png", "https://aes-corp.com/product/7788f-fire-subscriber/"),
        new("AES 7794 IntelliPro Fire", "aes-7794-intellipro.jpg", "https://aes-corp.com/product/7794-subscriber-add-on-module/"),
        new("AES 7794A IntelliPro 2.0", "aes-7794a-intellipro.png", "https://aes-corp.com/product/7794a-accessory-board/"),
        .. AntennaCatalog.All.Select(option => new HardwareAsset(
            $"AES {option.PartNumber} · {option.GainDb:0.#} dB · {option.Name}",
            option.ImageFile,
            option.ProductUrl))
    ];

    private readonly ICompanionSession _session;
    private int _searchStart;
    private bool _galleryBuilt;

    public TrainingPage(ICompanionSession session)
    {
        InitializeComponent();
        _session = session;
        GuidePicker.ItemsSource = Guides.ToArray();
        GuidePicker.ItemDisplayBinding = new Binding(nameof(TrainingGuide.Title));
        GuidePicker.SelectedIndex = 0;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (DocumentEditor.Text is null && GuidePicker.SelectedItem is TrainingGuide guide)
        {
            await LoadGuideAsync(guide);
        }

        if (!_galleryBuilt)
        {
            _galleryBuilt = true;
            await BuildGalleryAsync();
        }
    }

    private async void OnGuideChanged(object? sender, EventArgs args)
    {
        if (GuidePicker.SelectedItem is TrainingGuide guide)
        {
            await LoadGuideAsync(guide);
        }
    }

    private async Task LoadGuideAsync(TrainingGuide guide)
    {
        try
        {
            DocumentTitleLabel.Text = guide.Title;
            DocumentEditor.Text = await PackagedAssetService.ReadTextAsync($"Training/{guide.TextFile}");
            _searchStart = 0;
            SearchStatusLabel.Text = string.Empty;
            _session.RecordActivity($"Training guide opened · {guide.Title}");
        }
        catch (IOException exception)
        {
            DocumentEditor.Text = $"Unable to load the packaged guide text: {exception.Message}";
        }
    }

    private void OnFindClicked(object? sender, EventArgs args)
    {
        var term = SearchEntry.Text?.Trim() ?? string.Empty;
        var text = DocumentEditor.Text ?? string.Empty;
        if (term.Length == 0)
        {
            SearchStatusLabel.Text = "Enter text to find.";
            return;
        }

        var index = text.IndexOf(term, _searchStart, StringComparison.OrdinalIgnoreCase);
        if (index < 0 && _searchStart > 0)
        {
            index = text.IndexOf(term, StringComparison.OrdinalIgnoreCase);
        }
        if (index < 0)
        {
            SearchStatusLabel.Text = $"“{term}” was not found.";
            return;
        }

        DocumentEditor.CursorPosition = index;
        DocumentEditor.SelectionLength = term.Length;
        DocumentEditor.Focus();
        _searchStart = index + term.Length;
        SearchStatusLabel.Text = $"Found at character {index + 1:N0}.";
        _session.RecordActivity("Training text search completed");
    }

    private async void OnOpenPdfClicked(object? sender, EventArgs args)
    {
        if (GuidePicker.SelectedItem is not TrainingGuide guide)
        {
            return;
        }

        try
        {
            var path = await PackagedAssetService.MaterializeAsync($"Training/{guide.PdfFile}");
            _session.RecordActivity($"Original training PDF opened · {guide.Title}");
            await Launcher.Default.OpenAsync(
                new OpenFileRequest { File = new ReadOnlyFile(path) });
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or NotSupportedException)
        {
            await DisplayAlertAsync("Unable to open PDF", exception.Message, "OK");
        }
    }

    private async void OnSharePdfClicked(object? sender, EventArgs args)
    {
        if (GuidePicker.SelectedItem is not TrainingGuide guide)
        {
            return;
        }

        try
        {
            var path = await PackagedAssetService.MaterializeAsync($"Training/{guide.PdfFile}");
            _session.RecordActivity($"Original training PDF shared · {guide.Title}");
            await Share.Default.RequestAsync(
                new ShareFileRequest(guide.Title, new ShareFile(path)));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            await DisplayAlertAsync("Unable to share PDF", exception.Message, "OK");
        }
    }

    private async Task BuildGalleryAsync()
    {
        foreach (var asset in Hardware)
        {
            try
            {
                var thumbnail = new ImageButton
                {
                    Source = await PackagedAssetService.LoadImageAsync($"Hardware/{asset.ImageFile}"),
                    HeightRequest = 180,
                    Aspect = Aspect.AspectFit,
                    BackgroundColor = Colors.White,
                    CommandParameter = asset
                };
                thumbnail.Clicked += OnHardwareClicked;
                HardwareGallery.Children.Add(new Border
                {
                    Padding = 12,
                    BackgroundColor = Colors.White,
                    Stroke = Color.FromArgb("#DDE4EA"),
                    StrokeShape = new RoundRectangle { CornerRadius = new CornerRadius(10) },
                    Content = new VerticalStackLayout
                    {
                        Spacing = 8,
                        Children =
                        {
                            thumbnail,
                            new Label
                            {
                                Text = asset.Title,
                                FontAttributes = FontAttributes.Bold,
                                HorizontalTextAlignment = TextAlignment.Center
                            },
                            new Label
                            {
                                Text = "Tap image to enlarge and open the official AES product page.",
                                FontSize = 12,
                                TextColor = Color.FromArgb("#68788B"),
                                HorizontalTextAlignment = TextAlignment.Center
                            }
                        }
                    }
                });
            }
            catch (IOException)
            {
                HardwareGallery.Children.Add(new Label { Text = $"{asset.Title} image is unavailable." });
            }
        }
    }

    private async void OnHardwareClicked(object? sender, EventArgs args)
    {
        if (sender is not ImageButton { CommandParameter: HardwareAsset asset })
        {
            return;
        }

        var source = await PackagedAssetService.LoadImageAsync($"Hardware/{asset.ImageFile}");
        var resources = Application.Current?.Resources
            ?? throw new InvalidOperationException("Application resources are unavailable.");
        var officialButton = new Button { Text = "Open official AES product page" };
        officialButton.Clicked += async (_, _) =>
        {
            _session.RecordActivity($"Official AES product link opened · {asset.Title}");
            await Browser.Default.OpenAsync(asset.ProductUrl, BrowserLaunchMode.SystemPreferred);
        };
        var closeButton = new Button { Text = "Close", Style = (Style)resources["SecondaryButton"] };
        var page = new ContentPage
        {
            Title = asset.Title,
            BackgroundColor = Color.FromArgb("#F2F5F7"),
            Content = new ScrollView
            {
                Content = new VerticalStackLayout
                {
                    Padding = 20,
                    Spacing = 14,
                    Children =
                    {
                        new Label { Text = asset.Title, Style = (Style)resources["PageTitle"] },
                        new Image { Source = source, HeightRequest = 520, Aspect = Aspect.AspectFit },
                        new Entry
                        {
                            Text = asset.ProductUrl,
                            IsReadOnly = true,
                            FontSize = 12,
                            Placeholder = "Official AES product URL"
                        },
                        officialButton,
                        closeButton,
                        new SuperiorAes.Android.Controls.BrandFooterView()
                    }
                }
            }
        };
        closeButton.Clicked += async (_, _) => await page.Navigation.PopModalAsync();
        _session.RecordActivity($"Hardware image expanded · {asset.Title}");
        await Navigation.PushModalAsync(page);
    }

    private sealed record TrainingGuide(string Title, string PdfFile, string TextFile);
    private sealed record HardwareAsset(string Title, string ImageFile, string ProductUrl);
}
