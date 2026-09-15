namespace CasinoTracker.Services;

public sealed class SettingsService : ISettingsService
{
    private const string DarkModeKey = "settings.dark_mode";
    private const string AutoStopKey = "settings.auto_stop_hours";
    private const int DefaultAutoStopHours = 12;

    private readonly IPreferences _preferences;

    public SettingsService(IPreferences preferences)
    {
        _preferences = preferences;
    }

    public int MinAutoStopHours => 4;
    public int MaxAutoStopHours => 24;

    public bool IsDarkMode
    {
        get => _preferences.Get(DarkModeKey, false);
        set
        {
            _preferences.Set(DarkModeKey, value);
            ApplyTheme();
        }
    }

    public int AutoStopHours
    {
        get => Math.Clamp(_preferences.Get(AutoStopKey, DefaultAutoStopHours), MinAutoStopHours, MaxAutoStopHours);
        set => _preferences.Set(AutoStopKey, Math.Clamp(value, MinAutoStopHours, MaxAutoStopHours));
    }

    public void ApplyTheme()
    {
        if (Application.Current is { } app)
            app.UserAppTheme = IsDarkMode ? AppTheme.Dark : AppTheme.Light;
    }
}
