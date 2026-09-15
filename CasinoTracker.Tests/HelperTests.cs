using System.Globalization;
using CasinoTracker.Helpers;
using Xunit;

namespace CasinoTracker.Tests;

public class MoneyHelperTests
{
    [Theory]
    [InlineData("12.5", 12.5)]
    [InlineData("12,5", 12.5)]
    [InlineData("1,000.50", 1000.5)]
    [InlineData("1.000,50", 1000.5)]
    [InlineData("-20", -20)]
    [InlineData(" 42 ", 42)]
    [InlineData("100", 100)]
    [InlineData("0.01", 0.01)]
    public void TryParse_AcceptsCommonFormats(string input, decimal expected)
    {
        Assert.True(MoneyHelper.TryParse(input, out var value));
        Assert.Equal(expected, value);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    [InlineData("abc")]
    [InlineData("-")]
    public void TryParse_RejectsGarbage(string? input)
    {
        Assert.False(MoneyHelper.TryParse(input, out _));
    }

    [Theory]
    [InlineData("−50")]   // minus sign
    [InlineData("–50")]   // en dash
    [InlineData("‐50")]   // hyphen
    public void TryParse_AcceptsNonAsciiMinusSigns(string input)
    {
        Assert.True(MoneyHelper.TryParse(input, out var value));
        Assert.Equal(-50m, value);
    }

    [Fact]
    public void FormatSigned_RoundTripsThroughTryParse_ForEveryCulture()
    {
        foreach (var culture in new[] { "de-DE", "en-US", "fr-CH", "sv-SE" })
        {
            var previous = CultureInfo.CurrentCulture;
            CultureInfo.CurrentCulture = new CultureInfo(culture);
            try
            {
                var text = MoneyHelper.FormatSigned(-1234.5m);
                Assert.True(MoneyHelper.TryParse(text, out var value), $"{culture}: {text}");
                Assert.Equal(-1234.5m, value);
            }
            finally
            {
                CultureInfo.CurrentCulture = previous;
            }
        }
    }

    [Fact]
    public void FormatSigned_AddsPlusForPositive()
    {
        Assert.StartsWith("+", MoneyHelper.FormatSigned(5m));
        Assert.StartsWith("-", MoneyHelper.FormatSigned(-5m));
        Assert.DoesNotContain("+", MoneyHelper.FormatSigned(0m));
    }
}

public class TimeHelperTests
{
    [Fact]
    public void FormatDuration_UsesHoursAndMinutesAboveOneHour()
    {
        Assert.Equal("3h 05m", TimeHelper.FormatDuration(new TimeSpan(3, 5, 9)));
        Assert.Equal("12m 30s", TimeHelper.FormatDuration(new TimeSpan(0, 12, 30)));
    }

    [Fact]
    public void FormatClock_IsZeroPadded()
    {
        Assert.Equal("01:02:03", TimeHelper.FormatClock(new TimeSpan(1, 2, 3)));
        Assert.Equal("00:00:00", TimeHelper.FormatClock(TimeSpan.FromSeconds(-5)));
    }

    [Fact]
    public void Duration_UsesEndWhenGiven_AndNeverGoesNegative()
    {
        Assert.Equal(TimeSpan.FromSeconds(90), TimeHelper.Duration(100, 190));
        Assert.Equal(TimeSpan.Zero, TimeHelper.Duration(200, 100));
    }

    [Fact]
    public void LocalRoundTrip()
    {
        var now = TimeHelper.Now();
        Assert.Equal(now, TimeHelper.FromLocal(TimeHelper.ToLocal(now)));
    }
}
