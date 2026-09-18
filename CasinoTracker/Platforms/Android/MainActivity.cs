using Android.App;
using Android.Content.PM;
using Android.OS;
using AndroidX.Core.View;
using MauiApplication = Microsoft.Maui.Controls.Application;

namespace CasinoTracker;

[Activity(Theme = "@style/Maui.SplashTheme", MainLauncher = true, LaunchMode = LaunchMode.SingleTop,
    ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode |
                           ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
public class MainActivity : MauiAppCompatActivity
{
    // Keep in sync with Resources/Styles/Colors.xaml
    private static readonly Android.Graphics.Color StatusBarLight = Android.Graphics.Color.ParseColor("#1B5E20"); // PrimaryDark
    private static readonly Android.Graphics.Color NavBarLight = Android.Graphics.Color.ParseColor("#F3F5F3");    // PageLight
    private static readonly Android.Graphics.Color BarsDark = Android.Graphics.Color.ParseColor("#0F1411");       // PageDark

    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);
        ApplySystemBars();
        if (MauiApplication.Current is { } app)
            app.RequestedThemeChanged += OnThemeChanged;
    }

    protected override void OnDestroy()
    {
        if (MauiApplication.Current is { } app)
            app.RequestedThemeChanged -= OnThemeChanged;
        base.OnDestroy();
    }

    private void OnThemeChanged(object? sender, AppThemeChangedEventArgs e) => ApplySystemBars();

    /// <summary>
    /// Colours the status bar (top) and navigation bar (bottom) to match the app theme.
    /// Status bar icons are always white for readability; navigation bar icons follow the theme.
    /// </summary>
    private void ApplySystemBars()
    {
        if (Window is null) return;

        var dark = MauiApplication.Current?.RequestedTheme == AppTheme.Dark;

#pragma warning disable CA1422 // SetStatusBarColor/SetNavigationBarColor are deprecated on API 35 but still honoured while edge-to-edge is opted out
        Window.SetStatusBarColor(dark ? BarsDark : StatusBarLight);
        Window.SetNavigationBarColor(dark ? BarsDark : NavBarLight);
#pragma warning restore CA1422

        var controller = WindowCompat.GetInsetsController(Window, Window.DecorView);
        controller.AppearanceLightStatusBars = false; // white time/battery icons
        controller.AppearanceLightNavigationBars = !dark;
    }
}
