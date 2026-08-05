using Microsoft.Extensions.DependencyInjection;
using SuperiorAes.Android.Pages;

namespace SuperiorAes.Android;

public partial class App : Application
{
    private readonly IServiceProvider _services;

    public App(IServiceProvider services)
    {
        InitializeComponent();
        _services = services;
    }

    protected override Window CreateWindow(IActivationState? activationState) =>
        new(_services.GetRequiredService<StartupPage>());
}
