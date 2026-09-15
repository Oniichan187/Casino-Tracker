using System.Globalization;

namespace CasinoTracker.Converters;

public sealed class InvertedBoolConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is bool b ? !b : true;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is bool b ? !b : false;
}

/// <summary>
/// Positive → green, negative → red, zero → normal text colour. The colours are picked for the
/// active theme at conversion time; list pages rebuild their rows on appearing, so a theme switch
/// is reflected as soon as a page is shown again.
/// </summary>
public sealed class SignToColorConverter : IValueConverter
{
    public Color PositiveLight { get; set; } = Color.FromArgb("#2E7D32");
    public Color PositiveDark { get; set; } = Color.FromArgb("#81C784");
    public Color NegativeLight { get; set; } = Color.FromArgb("#C62828");
    public Color NegativeDark { get; set; } = Color.FromArgb("#EF9A9A");
    public Color ZeroLight { get; set; } = Color.FromArgb("#1A1F1B");
    public Color ZeroDark { get; set; } = Color.FromArgb("#EDF0ED");

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var d = value switch
        {
            decimal m => (double)m,
            double x => x,
            float f => f,
            int i => i,
            long l => l,
            _ => 0d,
        };

        var dark = Application.Current?.RequestedTheme == AppTheme.Dark;
        if (d > 0) return dark ? PositiveDark : PositiveLight;
        if (d < 0) return dark ? NegativeDark : NegativeLight;
        return dark ? ZeroDark : ZeroLight;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

public sealed class IsNotNullConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) => value is not null;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

public sealed class IsNullConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) => value is null;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

public sealed class StringNotEmptyConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is string s && !string.IsNullOrWhiteSpace(s);

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

public sealed class CountToBoolConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is int i ? i > 0 : value is System.Collections.ICollection c && c.Count > 0;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
