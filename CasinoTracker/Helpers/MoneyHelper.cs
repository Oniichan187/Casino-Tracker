using System.Globalization;

namespace CasinoTracker.Helpers;

public static class MoneyHelper
{
    public static string Format(decimal value) => value.ToString("N2", CultureInfo.CurrentCulture);

    public static string FormatSigned(decimal value) => (value > 0 ? "+" : string.Empty) + Format(value);

    /// <summary>
    /// Parses user input tolerant to both "." and "," as decimal separator,
    /// so that "12.5", "12,5", "1,000.50" and "1.000,50" all work.
    /// </summary>
    public static bool TryParse(string? text, out decimal value)
    {
        value = 0m;
        if (string.IsNullOrWhiteSpace(text)) return false;

        // Some cultures and keyboards use a real minus sign or a dash instead of the ASCII hyphen.
        // Without this the sign would simply be dropped and a loss would turn into a win.
        var normalised = text
            .Replace('−', '-')  // minus sign
            .Replace('–', '-')  // en dash
            .Replace('‐', '-'); // hyphen

        var cleaned = new string(normalised.Where(c => char.IsDigit(c) || c is '.' or ',' or '-').ToArray());
        if (cleaned.Length == 0) return false;

        int lastDot = cleaned.LastIndexOf('.');
        int lastComma = cleaned.LastIndexOf(',');
        if (lastDot >= 0 && lastComma >= 0)
        {
            // Both present: the last one is the decimal separator, the other one a grouping character.
            var grouping = lastDot > lastComma ? "," : ".";
            cleaned = cleaned.Replace(grouping, string.Empty);
        }
        cleaned = cleaned.Replace(',', '.');

        return decimal.TryParse(cleaned, NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingSign,
            CultureInfo.InvariantCulture, out value);
    }
}
