using Microsoft.Extensions.DependencyInjection;
using SuperiorAes.Android.Pages;
using SuperiorAes.Android.Platforms.Android;
using SuperiorAes.Android.Services;

namespace SuperiorAes.Android;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .ConfigureFonts(fonts =>
            {
                // Uses Android system fonts until approved brand font assets are supplied.
            });

        builder.Services.AddSingleton<SimulatedAesTransport>();
        builder.Services.AddSingleton<FtdiUsbTransport>();
        builder.Services.AddSingleton<IFtdiUsbTransport>(
            services => services.GetRequiredService<FtdiUsbTransport>());
        builder.Services.AddSingleton<IFtdiD2xxTransport>(
            services => services.GetRequiredService<FtdiUsbTransport>());
        builder.Services.AddSingleton<SelectableAesTransport>();
        builder.Services.AddSingleton<IAesTransportSelector>(
            services => services.GetRequiredService<SelectableAesTransport>());
        builder.Services.AddSingleton<IAesTransport>(
            services => services.GetRequiredService<SelectableAesTransport>());
        builder.Services.AddSingleton<ICompanionSession, CompanionSession>();
        builder.Services.AddSingleton<CredentialMigrationService>();
        builder.Services.AddSingleton<AppShell>();
        builder.Services.AddSingleton<StartupPage>();

        builder.Services.AddSingleton<DashboardPage>();
        builder.Services.AddSingleton<ProgrammingPage>();
        builder.Services.AddSingleton<ContactIdPage>();
        builder.Services.AddSingleton<DiagnosticsPage>();
        builder.Services.AddSingleton<RfMonitorPage>();
        builder.Services.AddSingleton<TroubleshooterPage>();
        builder.Services.AddSingleton<SitePlanningPage>();
        builder.Services.AddSingleton<MeshPage>();
        builder.Services.AddSingleton<TrainingPage>();
        builder.Services.AddSingleton<ReportsPage>();
        builder.Services.AddSingleton<ConfigurationPage>();
        builder.Services.AddSingleton<TerminalPage>();

        return builder.Build();
    }
}
