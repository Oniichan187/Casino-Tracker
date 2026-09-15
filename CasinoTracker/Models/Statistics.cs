namespace CasinoTracker.Models;

public readonly record struct TimePoint(long Timestamp, decimal Value);

public sealed class ItemStat
{
    public int MenuitemId { get; init; }
    public string Name { get; init; } = string.Empty;
    public bool IsBeverage { get; init; }
    public int Count { get; init; }
    public decimal Spend { get; init; }
}

public sealed class CasinoStat
{
    public int CasinoId { get; init; }
    public string Name { get; init; } = string.Empty;
    public int Sessions { get; init; }
    public TimeSpan Duration { get; init; }
    public decimal BuyIns { get; init; }
    public decimal CashOuts { get; init; }
    public decimal GameResult { get; init; }
    public decimal ConsumptionSpend { get; init; }
    public decimal Balance => GameResult - ConsumptionSpend;
    public List<ItemStat> Items { get; init; } = new();
}

public sealed class GameStat
{
    public int GameId { get; init; }
    public string Name { get; init; } = string.Empty;
    public int TimesPlayed { get; init; }
    public TimeSpan Duration { get; init; }
    public decimal Result { get; init; }
}

public sealed class SessionStat
{
    public int SessionId { get; init; }
    public string Name { get; init; } = string.Empty;
    public string CasinoName { get; init; } = string.Empty;
    public long Starttime { get; init; }
    public decimal Result { get; init; }
}

public sealed class StatisticsData
{
    public int TotalSessions { get; init; }

    /// <summary>Sessions with an end time. All duration based figures below only count these.</summary>
    public int FinishedSessions { get; init; }

    public TimeSpan TotalDuration { get; init; }
    public TimeSpan AverageDuration { get; init; }
    public TimeSpan LongestSession { get; init; }
    public decimal TotalGameResult { get; init; }
    public decimal TotalConsumption { get; init; }
    public decimal TotalBuyIns { get; init; }
    public decimal TotalCashOuts { get; init; }
    public decimal HourlyRate { get; init; }
    public double WinRate { get; init; }
    public SessionStat? BestSession { get; init; }
    public SessionStat? WorstSession { get; init; }
    public string? FavoriteCasino { get; init; }
    public string? MostPlayedGame { get; init; }
    public ItemStat? MostConsumedItem { get; init; }
    public List<CasinoStat> Casinos { get; init; } = new();
    public List<GameStat> Games { get; init; } = new();
    public List<ItemStat> Items { get; init; } = new();

    /// <summary>Cumulative game result over time (one point per session).</summary>
    public List<TimePoint> CumulativeResult { get; init; } = new();

    /// <summary>Cumulative game result minus consumption over time.</summary>
    public List<TimePoint> CumulativeBalance { get; init; } = new();

    /// <summary>Game result per month (Unix timestamp of month start).</summary>
    public List<TimePoint> MonthlyResult { get; init; } = new();
}
