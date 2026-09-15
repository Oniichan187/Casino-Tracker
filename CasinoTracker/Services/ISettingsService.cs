namespace CasinoTracker.Services;

public interface ISettingsService
{
    bool IsDarkMode { get; set; }

    /// <summary>Hours after which a running session is stopped automatically (4..24).</summary>
    int AutoStopHours { get; set; }

    int MinAutoStopHours { get; }
    int MaxAutoStopHours { get; }

    /// <summary>Applies the stored theme to the running application.</summary>
    void ApplyTheme();
}
