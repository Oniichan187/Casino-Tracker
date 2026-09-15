using CasinoTracker.Helpers;
using CasinoTracker.Models;

namespace CasinoTracker.Services;

public sealed class StatisticsService : IStatisticsService
{
    /// <summary>Upper bound for the monthly bar chart so it stays readable on a phone.</summary>
    public const int MaxMonths = 12;

    private readonly IDatabaseService _db;

    public StatisticsService(IDatabaseService db)
    {
        _db = db;
    }

    /// <summary>Aggregated numbers for one session.</summary>
    private sealed record SessionAgg(
        Session Session,
        TimeSpan Duration,
        decimal Result,
        decimal Consumption,
        decimal BuyIns,
        decimal CashOuts)
    {
        public bool IsFinished => Session.Endtime.HasValue;
    }

    public async Task<StatisticsData> ComputeAsync()
    {
        var conn = await _db.GetConnectionAsync();

        var sessions = await conn.Table<Session>().OrderBy(s => s.Starttime).ToListAsync();
        if (sessions.Count == 0) return new StatisticsData();

        var casinos = (await conn.Table<Casino>().ToListAsync()).ToDictionary(c => c.Id);
        var menuitems = (await conn.Table<Menuitem>().ToListAsync()).ToDictionary(m => m.Id);
        var games = (await conn.Table<Game>().ToListAsync()).ToDictionary(g => g.Id);
        var exchanges = (await conn.Table<Exchange>().ToListAsync()).ToLookup(e => e.SessionId);
        var played = (await conn.Table<PlayedGame>().ToListAsync()).ToLookup(g => g.SessionId);
        var consumed = (await conn.Table<ConsumedMenuitem>().ToListAsync()).ToLookup(c => c.SessionId);
        var prices = (await conn.Table<CasinoMenuitem>().ToListAsync())
            .GroupBy(p => p.CasinoId)
            .ToDictionary(g => g.Key, g => g.GroupBy(p => p.MenuitemId).ToDictionary(x => x.Key, x => x.First().Price));

        // Price of a consumed row: snapshot taken at consumption time, else the current menu price.
        decimal PriceOf(int casinoId, ConsumedMenuitem c) =>
            c.Price ?? (prices.TryGetValue(casinoId, out var map) && map.TryGetValue(c.MenuitemId, out var p) ? p : 0m);

        string CasinoName(int id) => casinos.TryGetValue(id, out var c) ? c.Name : "Unknown casino";
        string GameName(int id) => games.TryGetValue(id, out var g) ? g.Name : "Unknown game";
        string ItemName(int id) => menuitems.TryGetValue(id, out var m) ? m.Name : "Unknown item";
        bool IsBeverage(int id) => menuitems.TryGetValue(id, out var m) && m.Beverage;
        string SessionName(Session s) => string.IsNullOrWhiteSpace(s.Name) ? $"Session #{s.Id}" : s.Name;

        // ---- per session numbers
        var perSession = sessions.Select(s => new SessionAgg(
                s,
                TimeHelper.Duration(s.Starttime, s.Endtime),
                played[s.Id].Sum(g => g.Amount),
                consumed[s.Id].Sum(c => PriceOf(s.CasinoId, c)),
                exchanges[s.Id].Where(e => e.Amount > 0).Sum(e => e.Amount),
                -exchanges[s.Id].Where(e => e.Amount < 0).Sum(e => e.Amount)))
            .ToList();

        // Duration based figures only use finished sessions so a running session does not skew them.
        var finished = perSession.Where(p => p.IsFinished).ToList();
        var finishedDuration = TimeSpan.FromSeconds(finished.Sum(p => p.Duration.TotalSeconds));
        var finishedResult = finished.Sum(p => p.Result);
        var totalResult = perSession.Sum(p => p.Result);

        // ---- per casino
        var casinoStats = perSession
            .GroupBy(p => p.Session.CasinoId)
            .Select(g =>
            {
                var casinoId = g.Key;
                var items = g.SelectMany(p => consumed[p.Session.Id])
                    .GroupBy(c => c.MenuitemId)
                    .Select(ig => new ItemStat
                    {
                        MenuitemId = ig.Key,
                        Name = ItemName(ig.Key),
                        IsBeverage = IsBeverage(ig.Key),
                        Count = ig.Count(),
                        Spend = ig.Sum(c => PriceOf(casinoId, c)),
                    })
                    .OrderByDescending(i => i.Count)
                    .ThenBy(i => i.Name, StringComparer.CurrentCultureIgnoreCase)
                    .ToList();

                return new CasinoStat
                {
                    CasinoId = casinoId,
                    Name = CasinoName(casinoId),
                    Sessions = g.Count(),
                    Duration = TimeSpan.FromSeconds(g.Where(p => p.IsFinished).Sum(p => p.Duration.TotalSeconds)),
                    BuyIns = g.Sum(p => p.BuyIns),
                    CashOuts = g.Sum(p => p.CashOuts),
                    GameResult = g.Sum(p => p.Result),
                    ConsumptionSpend = g.Sum(p => p.Consumption),
                    Items = items,
                };
            })
            .OrderByDescending(c => c.Sessions)
            .ThenBy(c => c.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToList();

        // ---- per game (finished games only, a running game has no result yet)
        var gameStats = played.SelectMany(g => g)
            .Where(g => g.Endtime.HasValue)
            .GroupBy(g => g.GameId)
            .Select(g => new GameStat
            {
                GameId = g.Key,
                Name = GameName(g.Key),
                TimesPlayed = g.Count(),
                Duration = TimeSpan.FromSeconds(g.Sum(p => TimeHelper.Duration(p.Starttime, p.Endtime).TotalSeconds)),
                Result = g.Sum(p => p.Amount),
            })
            .OrderByDescending(g => g.TimesPlayed)
            .ThenBy(g => g.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToList();

        // ---- items overall
        var itemStats = perSession
            .SelectMany(p => consumed[p.Session.Id].Select(c => (p.Session.CasinoId, Row: c)))
            .GroupBy(x => x.Row.MenuitemId)
            .Select(g => new ItemStat
            {
                MenuitemId = g.Key,
                Name = ItemName(g.Key),
                IsBeverage = IsBeverage(g.Key),
                Count = g.Count(),
                Spend = g.Sum(x => PriceOf(x.CasinoId, x.Row)),
            })
            .OrderByDescending(i => i.Count)
            .ThenBy(i => i.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToList();

        // ---- time series (ordered by session start)
        var cumulativeResult = new List<TimePoint>();
        var cumulativeBalance = new List<TimePoint>();
        decimal runningResult = 0m, runningBalance = 0m;
        foreach (var p in perSession)
        {
            runningResult += p.Result;
            runningBalance += p.Result - p.Consumption;
            var ts = p.Session.Endtime ?? p.Session.Starttime;
            cumulativeResult.Add(new TimePoint(ts, runningResult));
            cumulativeBalance.Add(new TimePoint(ts, runningBalance));
        }

        // ---- monthly result, gaps filled with 0 so months without a visit are visible
        var byMonth = perSession
            .GroupBy(p =>
            {
                var d = TimeHelper.ToLocal(p.Session.Starttime);
                return new DateTime(d.Year, d.Month, 1);
            })
            .ToDictionary(g => g.Key, g => g.Sum(p => p.Result));
        var lastMonth = byMonth.Keys.Max();
        var firstMonth = byMonth.Keys.Min();
        if (firstMonth < lastMonth.AddMonths(-(MaxMonths - 1)))
            firstMonth = lastMonth.AddMonths(-(MaxMonths - 1));
        var monthly = new List<TimePoint>();
        for (var m = firstMonth; m <= lastMonth; m = m.AddMonths(1))
            monthly.Add(new TimePoint(TimeHelper.FromLocal(m), byMonth.TryGetValue(m, out var v) ? v : 0m));

        // ---- highlights
        var best = perSession.OrderByDescending(p => p.Result).First();
        var worst = perSession.OrderBy(p => p.Result).First();

        SessionStat ToStat(SessionAgg p) => new()
        {
            SessionId = p.Session.Id,
            Name = SessionName(p.Session),
            CasinoName = CasinoName(p.Session.CasinoId),
            Starttime = p.Session.Starttime,
            Result = p.Result,
        };

        return new StatisticsData
        {
            TotalSessions = sessions.Count,
            FinishedSessions = finished.Count,
            TotalDuration = finishedDuration,
            AverageDuration = finished.Count > 0 ? TimeSpan.FromSeconds(finishedDuration.TotalSeconds / finished.Count) : TimeSpan.Zero,
            LongestSession = finished.Count > 0 ? finished.Max(p => p.Duration) : TimeSpan.Zero,
            TotalGameResult = totalResult,
            TotalConsumption = perSession.Sum(p => p.Consumption),
            TotalBuyIns = perSession.Sum(p => p.BuyIns),
            TotalCashOuts = perSession.Sum(p => p.CashOuts),
            HourlyRate = finishedDuration.TotalHours > 0 ? finishedResult / (decimal)finishedDuration.TotalHours : 0m,
            WinRate = finished.Count > 0 ? finished.Count(p => p.Result > 0) / (double)finished.Count : 0d,
            BestSession = best.Result > 0 ? ToStat(best) : null,
            WorstSession = worst.Result < 0 ? ToStat(worst) : null,
            FavoriteCasino = casinoStats.FirstOrDefault()?.Name,
            MostPlayedGame = gameStats.FirstOrDefault()?.Name,
            MostConsumedItem = itemStats.FirstOrDefault(),
            Casinos = casinoStats,
            Games = gameStats,
            Items = itemStats,
            CumulativeResult = cumulativeResult,
            CumulativeBalance = cumulativeBalance,
            MonthlyResult = monthly,
        };
    }
}
