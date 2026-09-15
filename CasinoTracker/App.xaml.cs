using CasinoTracker.Services;

namespace CasinoTracker;

public partial class App : Application
{
    private readonly ISettingsService _settings;
    private readonly ISessionService _sessions;

    public App(ISettingsService settings, ISessionService sessions)
    {
        InitializeComponent();
        _settings = settings;
        _sessions = sessions;
        _settings.ApplyTheme();
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        var window = new Window(new AppShell());
        window.Resumed += OnResumed;
        return window;
    }

    /// <summary>Sessions that ran past the auto-stop limit while the app was in the background are closed here.</summary>
    private async void OnResumed(object? sender, EventArgs e)
    {
        try
        {
            await _sessions.AutoCloseExpiredSessionsAsync(TimeSpan.FromHours(_settings.AutoStopHours));
        }
        catch
        {
            // best effort – the main page performs the same check when it appears
        }
    }
}
