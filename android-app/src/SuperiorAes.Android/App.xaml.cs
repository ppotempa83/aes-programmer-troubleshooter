using SuperiorAes.Android.Pages;

namespace SuperiorAes.Android;

public partial class App : Application
{
    private readonly StartupPage _startupPage;

    public App(StartupPage startupPage)
    {
        InitializeComponent();
        _startupPage = startupPage;
    }

    protected override Window CreateWindow(IActivationState? activationState) =>
        new(_startupPage);
}
