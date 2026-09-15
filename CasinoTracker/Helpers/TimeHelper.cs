namespace CasinoTracker.Helpers;

public static class TimeHelper
{
    public static long Now() => DateTimeOffset.UtcNow.ToUnixTimeSeconds();

    public static DateTime ToLocal(long unixSeconds) =>
        DateTimeOffset.FromUnixTimeSeconds(unixSeconds).ToLocalTime().DateTime;

    public static long FromLocal(DateTime local) =>
        new DateTimeOffset(DateTime.SpecifyKind(local, DateTimeKind.Local)).ToUnixTimeSeconds();

    public static string FormatDateTime(long unixSeconds) => ToLocal(unixSeconds).ToString("dd.MM.yyyy HH:mm");
    public static string FormatDate(long unixSeconds) => ToLocal(unixSeconds).ToString("dd.MM.yyyy");
    public static string FormatTime(long unixSeconds) => ToLocal(unixSeconds).ToString("HH:mm");

    public static TimeSpan Duration(long start, long? end) =>
        TimeSpan.FromSeconds(Math.Max(0, (end ?? Now()) - start));

    /// <summary>"3h 05m" or "12m 30s" style.</summary>
    public static string FormatDuration(TimeSpan d)
    {
        if (d < TimeSpan.Zero) d = TimeSpan.Zero;
        if (d.TotalHours >= 1) return $"{(int)d.TotalHours}h {d.Minutes:00}m";
        return $"{d.Minutes}m {d.Seconds:00}s";
    }

    /// <summary>"01:23:45" style.</summary>
    public static string FormatClock(TimeSpan d)
    {
        if (d < TimeSpan.Zero) d = TimeSpan.Zero;
        return $"{(int)d.TotalHours:00}:{d.Minutes:00}:{d.Seconds:00}";
    }
}
