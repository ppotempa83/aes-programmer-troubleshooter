using System;
using System.Threading.Tasks;
using System.Windows;

namespace SuperiorAes.App;

public partial class App : Application
{
    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var splash = new SplashWindow();
        splash.Show();
        await Task.Delay(TimeSpan.FromSeconds(4));

        var mainWindow = new MainWindow();
        MainWindow = mainWindow;
        mainWindow.Show();
        splash.Close();
    }
}
