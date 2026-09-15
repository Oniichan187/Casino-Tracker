using CasinoTracker.Helpers;
using CasinoTracker.Models;
using CasinoTracker.Services;
using SQLite;
using Xunit;

namespace CasinoTracker.Tests;

public class StatisticsServiceTests : IAsyncLifetime
{
    private readonly TestDatabase _db = new();
    private SQLiteAsyncConnection _conn = null!;
    private StatisticsService _statistics = null!;

    private int _grand, _royal, _beer, _burger, _blackjack, _roulette;
    private const long T0 = 1_750_000_000; // fixed base timestamp

    public async Task InitializeAsync()
    {
        _conn = await _db.GetConnectionAsync();
        _statistics = new StatisticsService(_db);

        var casinos = new CasinoService(_db);
        var menuitems = new MenuitemService(_db);
        var games = new GameService(_db);

        _grand = await casinos.SaveAsync(new Casino { Name = "Grand" });
        _royal = await casinos.SaveAsync(new Casino { Name = "Royal" });
        _beer = await menuitems.SaveAsync(new Menuitem { Name = "Beer", Beverage = true });
        _burger = await menuitems.SaveAsync(new Menuitem { Name = "Burger", Beverage = false });
        _blackjack = await games.SaveAsync(new Game { Name = "Blackjack" });
        _roulette = await games.SaveAsync(new Game { Name = "Roulette" });

        await casinos.SetMenuAsync(_grand, new[] { (_beer, 5m), (_burger, 12m) });
        await casinos.SetMenuAsync(_royal, new[] { (_beer, 8m) });
    }

    public async Task DisposeAsync() => await _db.DisposeAsync();

    [Fact]
    public async Task EmptyDatabase_ReturnsEmptyStatistics()
    {
        var data = await _statistics.ComputeAsync();

        Assert.Equal(0, data.TotalSessions);
        Assert.Empty(data.Casinos);
        Assert.Empty(data.CumulativeResult);
        Assert.Null(data.BestSession);
    }

    [Fact]
    public async Task Aggregates_AreCorrect_ForSmallDataset()
    {
        // Session 1 @ Grand: 2h, buy-in 100, cash-out 130, result +30 (blackjack), 2 beers + 1 burger = 22
        var s1 = await Insert(new Session { CasinoId = _grand, Name = "S1", Starttime = T0, Endtime = T0 + 7200 });
        await Insert(new Exchange { SessionId = s1, Amount = 100, Timestamp = T0 + 10 });
        await Insert(new Exchange { SessionId = s1, Amount = -130, Timestamp = T0 + 7000 });
        await Insert(new PlayedGame { SessionId = s1, GameId = _blackjack, Amount = 30, Starttime = T0 + 100, Endtime = T0 + 3700 });
        await Insert(new ConsumedMenuitem { SessionId = s1, MenuitemId = _beer, Timestamp = T0 + 200 });
        await Insert(new ConsumedMenuitem { SessionId = s1, MenuitemId = _beer, Timestamp = T0 + 300 });
        await Insert(new ConsumedMenuitem { SessionId = s1, MenuitemId = _burger, Timestamp = T0 + 400 });

        // Session 2 @ Royal: 1h, buy-in 200, result -80 (roulette), 1 beer = 8
        var s2 = await Insert(new Session { CasinoId = _royal, Name = "S2", Starttime = T0 + 86400, Endtime = T0 + 86400 + 3600 });
        await Insert(new Exchange { SessionId = s2, Amount = 200, Timestamp = T0 + 86400 + 10 });
        await Insert(new PlayedGame { SessionId = s2, GameId = _roulette, Amount = -80, Starttime = T0 + 86400 + 100, Endtime = T0 + 86400 + 1900 });
        await Insert(new ConsumedMenuitem { SessionId = s2, MenuitemId = _beer, Timestamp = T0 + 86400 + 200 });

        // Session 3 @ Grand: 3h, buy-in 50, result -50 (blackjack) + 0 (roulette)
        var s3 = await Insert(new Session { CasinoId = _grand, Name = "S3", Starttime = T0 + 2 * 86400, Endtime = T0 + 2 * 86400 + 10800 });
        await Insert(new Exchange { SessionId = s3, Amount = 50, Timestamp = T0 + 2 * 86400 + 10 });
        await Insert(new PlayedGame { SessionId = s3, GameId = _blackjack, Amount = -50, Starttime = T0 + 2 * 86400 + 100, Endtime = T0 + 2 * 86400 + 1900 });
        await Insert(new PlayedGame { SessionId = s3, GameId = _roulette, Amount = 0, Starttime = T0 + 2 * 86400 + 2000, Endtime = T0 + 2 * 86400 + 2600 });

        var d = await _statistics.ComputeAsync();

        Assert.Equal(3, d.TotalSessions);
        Assert.Equal(TimeSpan.FromHours(6), d.TotalDuration);
        Assert.Equal(TimeSpan.FromHours(2), d.AverageDuration);
        Assert.Equal(TimeSpan.FromHours(3), d.LongestSession);
        Assert.Equal(-100m, d.TotalGameResult);
        Assert.Equal(30m, d.TotalConsumption);
        Assert.Equal(350m, d.TotalBuyIns);
        Assert.Equal(130m, d.TotalCashOuts);
        Assert.Equal(-100m / 6m, d.HourlyRate);
        Assert.Equal(1 / 3d, d.WinRate, 6);

        // per casino (Grand has more sessions -> first)
        Assert.Equal(new[] { "Grand", "Royal" }, d.Casinos.Select(c => c.Name).ToArray());
        var grand = d.Casinos[0];
        Assert.Equal(2, grand.Sessions);
        Assert.Equal(TimeSpan.FromHours(5), grand.Duration);
        Assert.Equal(150m, grand.BuyIns);
        Assert.Equal(130m, grand.CashOuts);
        Assert.Equal(-20m, grand.GameResult);
        Assert.Equal(22m, grand.ConsumptionSpend);
        Assert.Equal(-42m, grand.Balance);
        var grandBeer = grand.Items.Single(i => i.Name == "Beer");
        Assert.Equal(2, grandBeer.Count);
        Assert.Equal(10m, grandBeer.Spend);
        Assert.True(grandBeer.IsBeverage);
        Assert.Equal(12m, grand.Items.Single(i => i.Name == "Burger").Spend);

        var royal = d.Casinos[1];
        Assert.Equal(-80m, royal.GameResult);
        Assert.Equal(8m, royal.ConsumptionSpend);
        Assert.Equal(200m, royal.BuyIns);
        Assert.Equal(0m, royal.CashOuts);

        // per game (blackjack played 2x -> first)
        Assert.Equal("Blackjack", d.Games[0].Name);
        Assert.Equal(2, d.Games[0].TimesPlayed);
        Assert.Equal(-20m, d.Games[0].Result);
        Assert.Equal(TimeSpan.FromSeconds(3600 + 1800), d.Games[0].Duration);
        Assert.Equal("Roulette", d.Games[1].Name);
        Assert.Equal(-80m, d.Games[1].Result);

        // items overall
        Assert.Equal("Beer", d.Items[0].Name);
        Assert.Equal(3, d.Items[0].Count);
        Assert.Equal(18m, d.Items[0].Spend); // 2 x 5 + 1 x 8
        Assert.Equal("Beer", d.MostConsumedItem!.Name);

        // time series ordered by session start, cumulative
        Assert.Equal(new[] { 30m, -50m, -100m }, d.CumulativeResult.Select(p => p.Value).ToArray());
        Assert.Equal(new[] { 8m, -80m, -130m }, d.CumulativeBalance.Select(p => p.Value).ToArray());
        Assert.Equal(new long[] { T0 + 7200, T0 + 86400 + 3600, T0 + 2 * 86400 + 10800 }, d.CumulativeResult.Select(p => p.Timestamp).ToArray());

        // highlights
        Assert.Equal("S1", d.BestSession!.Name);
        Assert.Equal(30m, d.BestSession.Result);
        Assert.Equal("S2", d.WorstSession!.Name);
        Assert.Equal("Grand", d.FavoriteCasino);
        Assert.Equal("Blackjack", d.MostPlayedGame);

        // monthly result sums per calendar month (all three sessions are within 3 days)
        Assert.True(d.MonthlyResult.Count is 1 or 2);
        Assert.Equal(-100m, d.MonthlyResult.Sum(m => m.Value));
    }

    [Fact]
    public async Task RunningSession_CountsForResults_ButNotForDurationBasedFigures()
    {
        var start = TimeHelper.Now() - 1800;
        var s1 = await Insert(new Session { CasinoId = _grand, Name = "running", Starttime = start, Endtime = null });
        await Insert(new PlayedGame { SessionId = s1, GameId = _blackjack, Amount = 40, Starttime = start + 10, Endtime = start + 600 });
        await Insert(new PlayedGame { SessionId = s1, GameId = _roulette, Amount = 0, Starttime = start + 700, Endtime = null });

        var d = await _statistics.ComputeAsync();

        Assert.Equal(1, d.TotalSessions);
        Assert.Equal(0, d.FinishedSessions);
        Assert.Equal(TimeSpan.Zero, d.TotalDuration);
        Assert.Equal(TimeSpan.Zero, d.AverageDuration);
        Assert.Equal(0m, d.HourlyRate);
        Assert.Equal(0d, d.WinRate);
        Assert.Equal(40m, d.TotalGameResult);
        Assert.Equal("running", d.BestSession!.Name);
        Assert.Null(d.WorstSession);

        // only the finished game is counted, the running one has no result yet
        Assert.Single(d.Games);
        Assert.Equal("Blackjack", d.Games[0].Name);
        Assert.Equal(TimeSpan.FromSeconds(590), d.Games[0].Duration);
    }

    [Fact]
    public async Task ConsumptionSpend_UsesSnapshotPrice_WhenPresent()
    {
        var s1 = await Insert(new Session { CasinoId = _grand, Name = "S1", Starttime = T0, Endtime = T0 + 3600 });
        await Insert(new ConsumedMenuitem { SessionId = s1, MenuitemId = _beer, Timestamp = T0 + 100, Price = 4m }); // was cheaper back then
        await Insert(new ConsumedMenuitem { SessionId = s1, MenuitemId = _beer, Timestamp = T0 + 200, Price = null }); // legacy row -> current price 5

        var d = await _statistics.ComputeAsync();

        Assert.Equal(9m, d.TotalConsumption);
        Assert.Equal(9m, d.Casinos.Single().ConsumptionSpend);
        Assert.Equal(9m, d.Items.Single().Spend);
    }

    [Fact]
    public async Task MonthlyResult_FillsGapsWithZero_AndIsCapped()
    {
        var jan = new DateTime(2025, 1, 15, 20, 0, 0);
        var apr = new DateTime(2025, 4, 3, 20, 0, 0);
        var s1 = await Insert(new Session { CasinoId = _grand, Name = "Jan", Starttime = TimeHelper.FromLocal(jan), Endtime = TimeHelper.FromLocal(jan.AddHours(2)) });
        var s2 = await Insert(new Session { CasinoId = _grand, Name = "Apr", Starttime = TimeHelper.FromLocal(apr), Endtime = TimeHelper.FromLocal(apr.AddHours(2)) });
        await Insert(new PlayedGame { SessionId = s1, GameId = _blackjack, Amount = -70, Starttime = TimeHelper.FromLocal(jan), Endtime = TimeHelper.FromLocal(jan.AddHours(1)) });
        await Insert(new PlayedGame { SessionId = s2, GameId = _blackjack, Amount = 60, Starttime = TimeHelper.FromLocal(apr), Endtime = TimeHelper.FromLocal(apr.AddHours(1)) });

        var d = await _statistics.ComputeAsync();

        Assert.Equal(4, d.MonthlyResult.Count);
        Assert.Equal(new[] { -70m, 0m, 0m, 60m }, d.MonthlyResult.Select(m => m.Value).ToArray());
        Assert.Equal(new[] { 1, 2, 3, 4 }, d.MonthlyResult.Select(m => TimeHelper.ToLocal(m.Timestamp).Month).ToArray());

        // a very old session is dropped from the (capped) monthly chart but still counted elsewhere
        var old = new DateTime(2020, 6, 1, 20, 0, 0);
        await Insert(new Session { CasinoId = _grand, Name = "Old", Starttime = TimeHelper.FromLocal(old), Endtime = TimeHelper.FromLocal(old.AddHours(1)) });
        d = await _statistics.ComputeAsync();
        Assert.Equal(StatisticsService.MaxMonths, d.MonthlyResult.Count);
        Assert.Equal(3, d.TotalSessions);
    }

    [Fact]
    public async Task BlankSessionName_FallsBackToId_InHighlights()
    {
        var s1 = await Insert(new Session { CasinoId = _grand, Name = "  ", Starttime = T0, Endtime = T0 + 3600 });
        await Insert(new PlayedGame { SessionId = s1, GameId = _blackjack, Amount = 25, Starttime = T0 + 10, Endtime = T0 + 600 });

        var d = await _statistics.ComputeAsync();

        Assert.Equal($"Session #{s1}", d.BestSession!.Name);
    }

    private async Task<int> Insert<T>(T entity) where T : notnull
    {
        await _conn.InsertAsync(entity);
        return entity switch
        {
            Session s => s.Id,
            Exchange e => e.Id,
            PlayedGame p => p.Id,
            ConsumedMenuitem c => c.Id,
            _ => 0,
        };
    }
}
