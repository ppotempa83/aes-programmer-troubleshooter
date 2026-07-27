using Microsoft.Extensions.DependencyInjection;
using Microsoft.Maui;
using SuperiorAes.Android.Pages;
using SuperiorAes.Android.Services;

namespace SuperiorAes.Android;

public partial class AppShell : Shell
{
    private readonly ICompanionSession _session;

    public AppShell(IServiceProvider services)
    {
        InitializeComponent();
        _session = services.GetRequiredService<ICompanionSession>();
        Navigated += (_, args) =>
            _session.RecordActivity($"Page navigation · {args.Current.Location.OriginalString}");

        AddPage("Dashboard", "dashboard", services.GetRequiredService<DashboardPage>());
        AddPage("Programming", "programming", services.GetRequiredService<ProgrammingPage>());
        AddPage("Contact ID", "contact-id", services.GetRequiredService<ContactIdPage>());
        AddPage("Status & routes", "diagnostics", services.GetRequiredService<DiagnosticsPage>());
        AddPage("RF monitor", "rf-monitor", services.GetRequiredService<RfMonitorPage>());
        AddPage("Troubleshooter", "troubleshooter", services.GetRequiredService<TroubleshooterPage>());
        AddPage("Site & new radio", "site-planning", services.GetRequiredService<SitePlanningPage>());
        AddPage("Mesh simulator", "mesh", services.GetRequiredService<MeshPage>());
        AddPage("Training", "training", services.GetRequiredService<TrainingPage>());
        AddPage("Reports", "reports", services.GetRequiredService<ReportsPage>());
        AddPage("Configuration", "configuration", services.GetRequiredService<ConfigurationPage>());
        AddPage("Terminal", "terminal", services.GetRequiredService<TerminalPage>());
    }

    private void AddPage(string title, string route, Page page)
    {
        WireActivityLogging(page);
        var content = new ShellContent
        {
            Title = title,
            Route = $"{route}-content",
            Content = page
        };
        var item = new FlyoutItem
        {
            Title = title,
            Route = route
        };
        item.Items.Add(content);
        Items.Add(item);
    }

    private void WireActivityLogging(IVisualTreeElement element)
    {
        switch (element)
        {
            case Button button:
                button.Clicked += (_, _) =>
                    _session.RecordActivity($"UI button · {SafeLabel(button.Text, "button")}");
                break;
            case Entry entry:
                entry.Unfocused += (_, _) =>
                    _session.RecordActivity($"UI field edited · {SafeLabel(entry.Placeholder, "entry")}");
                break;
            case Editor editor:
                editor.Unfocused += (_, _) =>
                    _session.RecordActivity($"UI field edited · {SafeLabel(editor.Placeholder, "editor")}");
                break;
            case Picker picker:
                picker.SelectedIndexChanged += (_, _) =>
                    _session.RecordActivity($"UI selection changed · {SafeLabel(picker.Title, "picker")}");
                break;
            case CheckBox checkBox:
                checkBox.CheckedChanged += (_, args) =>
                    _session.RecordActivity($"UI option · check box · {(args.Value ? "selected" : "cleared")}");
                break;
            case Switch toggle:
                toggle.Toggled += (_, args) =>
                    _session.RecordActivity($"UI option · switch · {(args.Value ? "selected" : "cleared")}");
                break;
        }

        foreach (var child in element.GetVisualChildren())
        {
            WireActivityLogging(child);
        }
    }

    private static string SafeLabel(string? value, string fallback)
    {
        var normalized = value?.Trim().Replace("\r", " ").Replace("\n", " ");
        return string.IsNullOrWhiteSpace(normalized) ? fallback : normalized;
    }
}
