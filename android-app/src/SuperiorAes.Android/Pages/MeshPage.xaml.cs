using System.Globalization;
using Microsoft.Maui.Controls.Shapes;
using SuperiorAes.Android.Services;
using SuperiorAes.Core.Models;
using SuperiorAes.Core.Simulation;

namespace SuperiorAes.Android.Pages;

public partial class MeshPage : ContentPage
{
    private readonly ICompanionSession _session;
    private readonly VirtualMeshSimulator _simulator = new();
    private readonly MeshPathDrawable _pathDrawable = new();
    private readonly List<RadioEditor> _editors = [];
    private int _nextId = 1;

    public MeshPage(ICompanionSession session)
    {
        InitializeComponent();
        _session = session;
        MeshPathView.Drawable = _pathDrawable;
        SignalTypePicker.ItemsSource = new[]
        {
            "Fire alarm",
            "Trouble",
            "Supervisory",
            "Restoral",
            "Check-in"
        };
        SignalTypePicker.SelectedIndex = 0;
        var fieldRadio = new VirtualRadio
        {
            RadioId = "7740",
            Name = "Field radio",
            Model = AesModel.Aes7744F,
            NetCon = 3,
            LinkLayer = 1,
            Quality = "03"
        };
        var repeater = new VirtualRadio
        {
            RadioId = "AA11",
            Name = "Repeater A",
            Model = AesModel.Aes7788F,
            NetCon = 0,
            LinkLayer = 1,
            Quality = "03"
        };
        AddRadio(fieldRadio);
        AddRadio(repeater);
        UpdateVisualization([fieldRadio, repeater], null);
    }

    private void OnAddRadioClicked(object? sender, EventArgs args)
    {
        if (_editors.Count >= 4)
        {
            MeshOutputLabel.Text = "The virtual mesh is limited to four radios.";
            return;
        }

        AddRadio(new VirtualRadio
        {
            RadioId = $"{_nextId++:0000}",
            Name = $"Radio {_editors.Count + 1}",
            Model = AesModel.Aes7788F,
            NetCon = 3,
            LinkLayer = 1,
            Quality = "02"
        });
        _session.RecordActivity($"Virtual mesh radio added · {_editors.Count} configured");
    }

    private void AddRadio(VirtualRadio radio)
    {
        var modelPicker = new Picker
        {
            Title = "Model",
            ItemsSource = new[] { "7744F", "7788F" },
            SelectedIndex = radio.Model == AesModel.Aes7744F ? 0 : 1
        };
        var editor = new RadioEditor(
            new Entry { Text = radio.RadioId, MaxLength = 4, Placeholder = "ID" },
            new Entry { Text = radio.Name, Placeholder = "Name" },
            modelPicker,
            new Entry { Text = radio.NetCon.ToString(CultureInfo.InvariantCulture), Keyboard = Keyboard.Numeric, Placeholder = "N" },
            new Entry { Text = radio.LinkLayer.ToString(CultureInfo.InvariantCulture), Keyboard = Keyboard.Numeric, Placeholder = "L" },
            new Entry { Text = radio.Quality, MaxLength = 2, Placeholder = "Q" },
            new Switch { IsToggled = radio.Online });
        var remove = new Button
        {
            Text = "Remove",
            Style = (Style)(Application.Current?.Resources["DangerButton"]
                ?? throw new InvalidOperationException("Application resources are unavailable.")),
            CommandParameter = editor
        };
        remove.Clicked += OnRemoveRadioClicked;
        var fields = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Star)
            },
            RowDefinitions =
            {
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Auto)
            },
            ColumnSpacing = 6,
            RowSpacing = 6
        };
        fields.Add(editor.Id, 0, 0);
        fields.Add(editor.Name, 1, 0);
        fields.Add(editor.Model, 2, 0);
        fields.Add(editor.NetCon, 0, 1);
        fields.Add(editor.Layer, 1, 1);
        fields.Add(editor.Quality, 2, 1);
        fields.Add(new Label
        {
            Text = "Online",
            VerticalTextAlignment = TextAlignment.Center,
            FontSize = 12,
            TextColor = Color.FromArgb("#68788B")
        }, 0, 2);
        fields.Add(editor.Online, 1, 2);
        var card = new Border
        {
            Padding = 12,
            Stroke = Color.FromArgb("#DDE4EA"),
            BackgroundColor = Colors.White,
            StrokeShape = new RoundRectangle { CornerRadius = new CornerRadius(10) },
            Content = new VerticalStackLayout
            {
                Spacing = 8,
                Children =
                {
                    fields,
                    new Label { Text = "ID · name · model · NETCON · layer · Q · online", FontSize = 11, TextColor = Color.FromArgb("#68788B") },
                    remove
                }
            }
        };
        editor.Container = card;
        _editors.Add(editor);
        RadioList.Children.Add(card);
        RefreshPickers();
    }

    private void OnRemoveRadioClicked(object? sender, EventArgs args)
    {
        if (sender is not Button { CommandParameter: RadioEditor editor } || _editors.Count <= 1)
        {
            MeshOutputLabel.Text = "The simulator requires at least one radio.";
            return;
        }

        _editors.Remove(editor);
        RadioList.Children.Remove(editor.Container);
        RefreshPickers();
        _session.RecordActivity($"Virtual mesh radio removed · {_editors.Count} configured");
    }

    private async void OnSignalClicked(object? sender, EventArgs args) =>
        await SimulateAsync(1);

    private async void OnBurstClicked(object? sender, EventArgs args) =>
        await SimulateAsync(10);

    private async Task SimulateAsync(int count)
    {
        if (!TryReadRadios(out var radios) ||
            SourcePicker.SelectedItem is not string source ||
            DestinationPicker.SelectedItem is not string destination)
        {
            return;
        }

        var sourceId = ParsePickerId(source);
        var destinationId = destination == "BROADCAST" ? destination : ParsePickerId(destination);
        var signals = new List<VirtualMeshSignal>();
        try
        {
            for (var index = 0; index < count; index++)
            {
                signals.AddRange(_simulator.Send(
                    radios,
                    sourceId,
                    destinationId,
                    SignalTypePicker.SelectedItem?.ToString() ?? "Test"));
            }

            var success = signals.Count(signal => signal.Result.StartsWith("RECEIVED", StringComparison.Ordinal));
            MeshOutputLabel.Text =
                $"VIRTUAL ONLY · {success}/{signals.Count} received/ACK\n" +
                string.Join(
                    Environment.NewLine,
                    signals.TakeLast(20).Select(signal =>
                        $"{signal.Timestamp.LocalDateTime:[MM-dd-yyyy / hh:mm (tt)]} · {signal.Route} · {signal.Result} · {signal.ProbabilityPercent:0.#}%\n  {signal.Detail}"));
            UpdateVisualization(radios, signals.LastOrDefault());
            await _session.RunGuidedActionAsync(
                $"Virtual mesh {count}-packet simulation",
                $"{success}/{signals.Count} virtual path result(s) received/ACK; no RF transmission occurred.");
        }
        catch (ArgumentException exception)
        {
            MeshOutputLabel.Text = exception.Message;
        }
    }

    private bool TryReadRadios(out IReadOnlyList<VirtualRadio> radios)
    {
        var values = new List<VirtualRadio>();
        foreach (var editor in _editors)
        {
            var id = editor.Id.Text?.Trim().ToUpperInvariant() ?? string.Empty;
            var quality = editor.Quality.Text?.Trim().ToUpperInvariant() ?? string.Empty;
            if (id.Length != 4 || !id.All(Uri.IsHexDigit) ||
                !int.TryParse(editor.NetCon.Text, out var netcon) || netcon is < 0 or > 7 ||
                !int.TryParse(editor.Layer.Text, out var layer) || layer is < 0 or > 99 ||
                quality is not ("03" or "02" or "01" or "83" or "82" or "81"))
            {
                MeshOutputLabel.Text = "Each radio needs a unique four-hex ID, NETCON 0–7, layer 0–99, and Q 03/02/01/83/82/81.";
                radios = [];
                return false;
            }

            values.Add(new VirtualRadio
            {
                RadioId = id,
                Name = editor.Name.Text?.Trim() ?? id,
                Model = editor.Model.SelectedIndex == 0
                    ? AesModel.Aes7744F
                    : AesModel.Aes7788F,
                NetCon = netcon,
                LinkLayer = layer,
                Quality = quality,
                Online = editor.Online.IsToggled
            });
        }

        if (values.Select(value => value.RadioId).Distinct(StringComparer.OrdinalIgnoreCase).Count() != values.Count)
        {
            MeshOutputLabel.Text = "Virtual radio IDs must be unique.";
            radios = [];
            return false;
        }

        radios = values;
        RefreshPickers(values);
        return true;
    }

    private void RefreshPickers() =>
        RefreshPickers(
            _editors.Select(editor => new VirtualRadio
            {
                RadioId = editor.Id.Text?.Trim().ToUpperInvariant() ?? "????",
                Name = editor.Name.Text?.Trim() ?? "Radio"
            }).ToArray());

    private void RefreshPickers(IReadOnlyList<VirtualRadio> radios)
    {
        var previousSource = SourcePicker.SelectedItem?.ToString();
        var previousDestination = DestinationPicker.SelectedItem?.ToString();
        var labels = radios.Select(radio => $"{radio.RadioId} · {radio.Name}").ToArray();
        SourcePicker.ItemsSource = labels;
        DestinationPicker.ItemsSource = new[] { "BROADCAST" }.Concat(labels).ToArray();
        SourcePicker.SelectedItem = labels.FirstOrDefault(value => value == previousSource) ?? labels.FirstOrDefault();
        DestinationPicker.SelectedItem =
            DestinationPicker.ItemsSource.Cast<string>().FirstOrDefault(value => value == previousDestination) ??
            DestinationPicker.ItemsSource.Cast<string>().Skip(1).FirstOrDefault() ??
            "BROADCAST";
    }

    private static string ParsePickerId(string value) =>
        value.Split('·', StringSplitOptions.TrimEntries)[0];

    private void UpdateVisualization(
        IReadOnlyList<VirtualRadio> radios,
        VirtualMeshSignal? latestSignal)
    {
        _pathDrawable.Radios = radios
            .Select(radio => new VirtualRadio
            {
                RadioId = radio.RadioId,
                Name = radio.Name,
                Model = radio.Model,
                NetCon = radio.NetCon,
                LinkLayer = radio.LinkLayer,
                Quality = radio.Quality,
                Online = radio.Online
            })
            .ToArray();
        _pathDrawable.LatestSignal = latestSignal;
        MeshPathSummaryLabel.Text = latestSignal is null
            ? "Send a virtual signal to visualize its direct or repeated path."
            : $"{latestSignal.SignalType} · {latestSignal.Route} · {latestSignal.Result} · {latestSignal.ProbabilityPercent:0.#}%";
        MeshPathView.Invalidate();
    }

    private sealed record RadioEditor(
        Entry Id,
        Entry Name,
        Picker Model,
        Entry NetCon,
        Entry Layer,
        Entry Quality,
        Switch Online)
    {
        public Border Container { get; set; } = null!;
    }

    private sealed class MeshPathDrawable : IDrawable
    {
        public IReadOnlyList<VirtualRadio> Radios { get; set; } = [];
        public VirtualMeshSignal? LatestSignal { get; set; }

        public void Draw(ICanvas canvas, RectF dirtyRect)
        {
            canvas.FillColor = Color.FromArgb("#F4F7FA");
            canvas.FillRoundedRectangle(dirtyRect, 12);

            if (Radios.Count == 0)
            {
                return;
            }

            var inset = 52f;
            var width = Math.Max(1f, dirtyRect.Width - inset * 2);
            var height = Math.Max(1f, dirtyRect.Height - inset * 2);
            var centerX = dirtyRect.Center.X;
            var centerY = dirtyRect.Center.Y;
            var radiusX = width / 2;
            var radiusY = height / 2;
            var positions = new Dictionary<string, PointF>(StringComparer.OrdinalIgnoreCase);

            for (var index = 0; index < Radios.Count; index++)
            {
                var angle = -MathF.PI / 2 + index * (2 * MathF.PI / Radios.Count);
                positions[Radios[index].RadioId] = new PointF(
                    centerX + MathF.Cos(angle) * radiusX,
                    centerY + MathF.Sin(angle) * radiusY);
            }

            canvas.StrokeColor = Color.FromArgb("#CBD6E2");
            canvas.StrokeSize = 2;
            for (var first = 0; first < Radios.Count; first++)
            {
                for (var second = first + 1; second < Radios.Count; second++)
                {
                    var from = positions[Radios[first].RadioId];
                    var to = positions[Radios[second].RadioId];
                    canvas.DrawLine(from, to);
                }
            }

            if (LatestSignal is not null)
            {
                var routeIds = LatestSignal.Route.Split(
                    '→',
                    StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                canvas.StrokeColor = LatestSignal.Result.StartsWith("RECEIVED", StringComparison.Ordinal)
                    ? Color.FromArgb("#16825D")
                    : Color.FromArgb("#C5202F");
                canvas.StrokeSize = 6;
                for (var index = 0; index < routeIds.Length - 1; index++)
                {
                    if (positions.TryGetValue(routeIds[index], out var from) &&
                        positions.TryGetValue(routeIds[index + 1], out var to))
                    {
                        canvas.DrawLine(from, to);
                    }
                }
            }

            foreach (var radio in Radios)
            {
                var position = positions[radio.RadioId];
                canvas.FillColor = radio.Online
                    ? Color.FromArgb("#10253D")
                    : Color.FromArgb("#68788B");
                canvas.FillCircle(position, 34);
                canvas.StrokeColor = radio.Model == AesModel.Aes7744F
                    ? Color.FromArgb("#F2A900")
                    : Color.FromArgb("#C5202F");
                canvas.StrokeSize = 4;
                canvas.DrawCircle(position, 34);
                canvas.FontColor = Colors.White;
                canvas.FontSize = 11;
                canvas.Font = Microsoft.Maui.Graphics.Font.DefaultBold;
                canvas.DrawString(
                    $"{radio.RadioId}\n{(radio.Model == AesModel.Aes7744F ? "7744F" : "7788F")}",
                    new RectF(position.X - 32, position.Y - 20, 64, 40),
                    HorizontalAlignment.Center,
                    VerticalAlignment.Center);
            }
        }
    }
}
