using Android.App;
using Android.Content.PM;
using Android.Hardware.Usb;

namespace SuperiorAes.Android;

[IntentFilter(
    new[] { UsbManager.ActionUsbDeviceAttached },
    Categories = new[] { global::Android.Content.Intent.CategoryDefault })]
[MetaData(
    UsbManager.ActionUsbDeviceAttached,
    Resource = "@xml/ftdi_device_filter")]
[Activity(
    Theme = "@style/Maui.SplashTheme",
    MainLauncher = true,
    LaunchMode = LaunchMode.SingleTop,
    ConfigurationChanges =
        ConfigChanges.ScreenSize |
        ConfigChanges.Orientation |
        ConfigChanges.UiMode |
        ConfigChanges.ScreenLayout |
        ConfigChanges.SmallestScreenSize |
        ConfigChanges.Density)]
public sealed class MainActivity : MauiAppCompatActivity;
