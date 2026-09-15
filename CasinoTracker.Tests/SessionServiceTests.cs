using CasinoTracker.Helpers;
using CasinoTracker.Models;
using CasinoTracker.Services;
using Xunit;

namespace CasinoTracker.Tests;

public class SessionServiceTests : IAsyncLifetime
{
    private readonly TestDatabase _db = new();
    private SessionService _sessions = null!;
    private CasinoService _casinos = null!;
    private int _casinoId;
    private int _beerId;
    private int _blackjackId;

    public async Task InitializeAsync()
    {
        _sessions = new SessionService(_db);
        _casinos = new CasinoService(_db);
        var menuitems = new MenuitemService(_db);
        var games = new GameService(_db);

        _casinoId = await _casinos.SaveAsync(new Casino { Name = "Grand" });
        _beerId = await menuitems.SaveAsync(new Menuitem { Name = "Beer", Beverage = true });
        _blackjackId = await games.SaveAsync(new Game { Name = "Blackjack" });

        await _casinos.SetMenuAsync(_casinoId, new[] { (_beerId, 6.5m) });
        await _casinos.SetGamesAsync(_casinoId, new[] { _blackjackId });
    }

    public async Task DisposeAsync() => await _db.DisposeAsync();

    [Fact]
    public async Task StartSession_BecomesActive_WithGeneratedName()
    {
        var session = await _sessions.StartSessionAsync(_casinoId, null);

        var active = await _sessions.GetActiveSessionAsync();
        Assert.NotNull(active);
        Assert.Equal(session.Id, active!.Id);
        Assert.Null(active.Endtime);
        Assert.StartsWith("Grand ", active.Name);
    }

    [Fact]
    public async Task StartSession_UsesGivenName()
    {
        var session = await _sessions.StartSessionAsync(_casinoId, "  Friday night ");
        Assert.Equal("Friday night", session.Name);

        await _sessions.UpdateSessionNameAsync(session.Id, "Renamed");
        Assert.Equal("Renamed", (await _sessions.GetAsync(session.Id))!.Name);
    }

    [Fact]
    public async Task Bankroll_FollowsSpecScenario()
    {
        var session = await _sessions.StartSessionAsync(_casinoId, "x");

        await _sessions.AddExchangeAsync(session.Id, 100);
        var played = await _sessions.StartGameAsync(session.Id, _blackjackId);
        Assert.NotNull(await _sessions.GetOpenPlayedGameAsync(session.Id));

        await _sessions.StopGameAsync(played.Id, 50);
        Assert.Null(await _sessions.GetOpenPlayedGameAsync(session.Id));

        var afterWin = await _sessions.GetBankrollAsync(session.Id);
        Assert.Equal(150m, afterWin.Chips);
        Assert.Equal(0m, afterWin.Money);

        await _sessions.AddExchangeAsync(session.Id, -150);
        var afterCashOut = await _sessions.GetBankrollAsync(session.Id);
        Assert.Equal(0m, afterCashOut.Chips);
        Assert.Equal(150m, afterCashOut.Money);

        await _sessions.AddExchangeAsync(session.Id, 200);
        var afterRebuy = await _sessions.GetBankrollAsync(session.Id);
        Assert.Equal(200m, afterRebuy.Chips);
        Assert.Equal(0m, afterRebuy.Money);
        Assert.Equal(200m, afterRebuy.Bankroll);
    }

    [Fact]
    public async Task EndSession_ClosesOpenGames_AndClearsActive()
    {
        var session = await _sessions.StartSessionAsync(_casinoId, "x");
        await _sessions.StartGameAsync(session.Id, _blackjackId);

        await _sessions.EndSessionAsync(session.Id);

        Assert.Null(await _sessions.GetActiveSessionAsync());
        var stored = await _sessions.GetAsync(session.Id);
        Assert.NotNull(stored!.Endtime);
        var games = await _sessions.GetPlayedGamesAsync(session.Id);
        Assert.All(games, g => Assert.NotNull(g.Endtime));
        Assert.All(games, g => Assert.Equal(0m, g.Amount));
    }

    [Fact]
    public async Task EndSession_IsIdempotent()
    {
        var session = await _sessions.StartSessionAsync(_casinoId, "x");
        await _sessions.EndSessionAsync(session.Id, session.Starttime + 60);
        await _sessions.EndSessionAsync(session.Id, session.Starttime + 120);

        Assert.Equal(session.Starttime + 60, (await _sessions.GetAsync(session.Id))!.Endtime);
    }

    [Fact]
    public async Task EndSession_NeverEndsBeforeItStarted()
    {
        var session = await _sessions.StartSessionAsync(_casinoId, "x");
        await _sessions.EndSessionAsync(session.Id, session.Starttime - 3600);

        Assert.Equal(session.Starttime, (await _sessions.GetAsync(session.Id))!.Endtime);
    }

    [Fact]
    public async Task AutoClose_ClosesOnlyExpiredSessions_AtStartPlusLimit()
    {
        var conn = await _db.GetConnectionAsync();
        var now = TimeHelper.Now();
        var old = new Session { CasinoId = _casinoId, Name = "old", Starttime = now - 13 * 3600 };
        var fresh = new Session { CasinoId = _casinoId, Name = "fresh", Starttime = now - 3600 };
        await conn.InsertAsync(old);
        await conn.InsertAsync(fresh);
        await conn.InsertAsync(new PlayedGame { SessionId = old.Id, GameId = _blackjackId, Starttime = old.Starttime + 60 });

        var closed = await _sessions.AutoCloseExpiredSessionsAsync(TimeSpan.FromHours(12));

        Assert.Equal(1, closed);
        var storedOld = await _sessions.GetAsync(old.Id);
        Assert.Equal(old.Starttime + 12 * 3600, storedOld!.Endtime);
        var game = (await _sessions.GetPlayedGamesAsync(old.Id)).Single();
        Assert.Equal(old.Starttime + 12 * 3600, game.Endtime);

        var storedFresh = await _sessions.GetAsync(fresh.Id);
        Assert.Null(storedFresh!.Endtime);
        Assert.Equal(fresh.Id, (await _sessions.GetActiveSessionAsync())!.Id);
    }

    [Fact]
    public async Task Consumed_AddAndRemoveLast()
    {
        var session = await _sessions.StartSessionAsync(_casinoId, "x");

        await _sessions.AddConsumedAsync(session.Id, _beerId);
        await _sessions.AddConsumedAsync(session.Id, _beerId);
        Assert.Equal(2, (await _sessions.GetConsumedAsync(session.Id)).Count);

        Assert.True(await _sessions.RemoveLastConsumedAsync(session.Id, _beerId));
        Assert.Single(await _sessions.GetConsumedAsync(session.Id));

        Assert.True(await _sessions.RemoveLastConsumedAsync(session.Id, _beerId));
        Assert.False(await _sessions.RemoveLastConsumedAsync(session.Id, _beerId));
        Assert.Empty(await _sessions.GetConsumedAsync(session.Id));
    }

    [Fact]
    public async Task Summaries_And_Detail_ResolveNamesAndPrices()
    {
        var session = await _sessions.StartSessionAsync(_casinoId, "x");
        await _sessions.AddExchangeAsync(session.Id, 100);
        await _sessions.AddExchangeAsync(session.Id, -30);
        var played = await _sessions.StartGameAsync(session.Id, _blackjackId);
        await _sessions.StopGameAsync(played.Id, -25);
        await _sessions.AddConsumedAsync(session.Id, _beerId);
        await _sessions.AddConsumedAsync(session.Id, _beerId);

        var summary = (await _sessions.GetSummariesAsync()).Single();
        Assert.Equal("Grand", summary.CasinoName);
        Assert.Equal(100m, summary.BuyIns);
        Assert.Equal(30m, summary.CashOuts);
        Assert.Equal(-25m, summary.GameResult);
        Assert.Equal(13m, summary.ConsumptionSpend);
        Assert.Equal(2, summary.ConsumedCount);

        var detail = await _sessions.GetDetailAsync(session.Id);
        Assert.NotNull(detail);
        Assert.Equal("Grand", detail!.Casino!.Name);
        Assert.Equal(6.5m, detail.PriceOf(_beerId));
        Assert.Equal("Beer", detail.MenuitemName(_beerId));
        Assert.Equal("Blackjack", detail.GameName(_blackjackId));
        Assert.Equal(13m, detail.ConsumptionSpend);
        Assert.Equal(-25m, detail.GameResult);
        Assert.Equal(100m, detail.BuyIns);
        Assert.Equal(30m, detail.CashOuts);
    }

    [Fact]
    public async Task DeleteSession_RemovesChildren()
    {
        var session = await _sessions.StartSessionAsync(_casinoId, "x");
        await _sessions.AddExchangeAsync(session.Id, 100);
        await _sessions.StartGameAsync(session.Id, _blackjackId);
        await _sessions.AddConsumedAsync(session.Id, _beerId);

        await _sessions.DeleteSessionAsync(session.Id);

        Assert.Null(await _sessions.GetAsync(session.Id));
        Assert.Empty(await _sessions.GetExchangesAsync(session.Id));
        Assert.Empty(await _sessions.GetPlayedGamesAsync(session.Id));
        Assert.Empty(await _sessions.GetConsumedAsync(session.Id));
    }

    [Fact]
    public async Task ConsumedPrice_IsSnapshotted_AndSurvivesMenuChanges()
    {
        var session = await _sessions.StartSessionAsync(_casinoId, "x");
        await _sessions.AddConsumedAsync(session.Id, _beerId);

        // price change and removal from the menu must not rewrite history
        await _casinos.SetMenuAsync(_casinoId, new[] { (_beerId, 9m) });
        var afterPriceChange = (await _sessions.GetSummariesAsync()).Single();
        Assert.Equal(6.5m, afterPriceChange.ConsumptionSpend);

        await _casinos.SetMenuAsync(_casinoId, Array.Empty<(int, decimal)>());
        var afterUnlink = await _sessions.GetDetailAsync(session.Id);
        Assert.Equal(6.5m, afterUnlink!.ConsumptionSpend);
        Assert.Equal(6.5m, afterUnlink.PriceOf(afterUnlink.Consumed.Single()));
        Assert.Equal("Beer", afterUnlink.MenuitemName(_beerId));

        // rows without a snapshot (legacy) still fall back to the current menu price
        var conn = await _db.GetConnectionAsync();
        await conn.InsertAsync(new ConsumedMenuitem { SessionId = session.Id, MenuitemId = _beerId, Timestamp = TimeHelper.Now(), Price = null });
        await _casinos.SetMenuAsync(_casinoId, new[] { (_beerId, 2m) });
        var withLegacy = await _sessions.GetDetailAsync(session.Id);
        Assert.Equal(8.5m, withLegacy!.ConsumptionSpend);
    }

    [Fact]
    public async Task EndSession_NeverEndsBeforeAGameStarted()
    {
        var conn = await _db.GetConnectionAsync();
        var now = TimeHelper.Now();
        var session = new Session { CasinoId = _casinoId, Name = "x", Starttime = now - 13 * 3600 };
        await conn.InsertAsync(session);
        // a game that started after the auto-stop limit had already elapsed
        await conn.InsertAsync(new PlayedGame { SessionId = session.Id, GameId = _blackjackId, Starttime = now - 60 });

        await _sessions.AutoCloseExpiredSessionsAsync(TimeSpan.FromHours(12));

        var stored = await _sessions.GetAsync(session.Id);
        var game = (await _sessions.GetPlayedGamesAsync(session.Id)).Single();
        Assert.Equal(now - 60, stored!.Endtime);
        Assert.Equal(now - 60, game.Endtime);
    }

    [Fact]
    public async Task AutoClose_NeverEndsBeforeTheLastEntry()
    {
        var conn = await _db.GetConnectionAsync();
        var now = TimeHelper.Now();
        var session = new Session { CasinoId = _casinoId, Name = "x", Starttime = now - 13 * 3600 };
        await conn.InsertAsync(session);
        // entries made after the 12h limit had already elapsed
        await conn.InsertAsync(new Exchange { SessionId = session.Id, Amount = 50, Timestamp = now - 1800 });
        await conn.InsertAsync(new ConsumedMenuitem { SessionId = session.Id, MenuitemId = _beerId, Timestamp = now - 600 });

        await _sessions.AutoCloseExpiredSessionsAsync(TimeSpan.FromHours(12));

        Assert.Equal(now - 600, (await _sessions.GetAsync(session.Id))!.Endtime);
    }

    [Fact]
    public async Task UnpricedItem_IsSnapshottedAsZero_AndStaysZeroAfterAPriceIsAdded()
    {
        var menuitems = new MenuitemService(_db);
        var water = await menuitems.SaveAsync(new Menuitem { Name = "Water", Beverage = true });
        var session = await _sessions.StartSessionAsync(_casinoId, "x");

        // the item is not on this casino's menu yet
        var consumed = await _sessions.AddConsumedAsync(session.Id, water);
        Assert.Equal(0m, consumed.Price);

        await _casinos.SetMenuAsync(_casinoId, new[] { (_beerId, 6.5m), (water, 4m) });

        var detail = await _sessions.GetDetailAsync(session.Id);
        Assert.Equal(0m, detail!.ConsumptionSpend);
    }

    [Fact]
    public async Task Exchanges_CanBeEditedAndDeleted()
    {
        var session = await _sessions.StartSessionAsync(_casinoId, "x");
        var buyIn = await _sessions.AddExchangeAsync(session.Id, 1000);
        var cashOut = await _sessions.AddExchangeAsync(session.Id, -50);

        await _sessions.UpdateExchangeAmountAsync(buyIn.Id, 100);
        var bankroll = await _sessions.GetBankrollAsync(session.Id);
        Assert.Equal(50m, bankroll.Chips);
        Assert.Equal(50m, bankroll.Money);

        await _sessions.DeleteExchangeAsync(cashOut.Id);
        bankroll = await _sessions.GetBankrollAsync(session.Id);
        Assert.Equal(100m, bankroll.Chips);
        Assert.Equal(0m, bankroll.Money);
        Assert.Single(await _sessions.GetExchangesAsync(session.Id));
    }

    [Fact]
    public async Task PlayedGames_CanBeEditedAndDeleted()
    {
        var session = await _sessions.StartSessionAsync(_casinoId, "x");
        await _sessions.AddExchangeAsync(session.Id, 100);
        var first = await _sessions.StartGameAsync(session.Id, _blackjackId);
        await _sessions.StopGameAsync(first.Id, -30);
        var second = await _sessions.StartGameAsync(session.Id, _blackjackId);

        await _sessions.UpdatePlayedGameAmountAsync(first.Id, 20);
        Assert.Equal(120m, (await _sessions.GetBankrollAsync(session.Id)).Chips);

        // discarding the running game clears the open game
        await _sessions.DeletePlayedGameAsync(second.Id);
        Assert.Null(await _sessions.GetOpenPlayedGameAsync(session.Id));
        Assert.Single(await _sessions.GetPlayedGamesAsync(session.Id));
    }

    [Fact]
    public async Task Consumed_CanBeDeletedById()
    {
        var session = await _sessions.StartSessionAsync(_casinoId, "x");
        var consumed = await _sessions.AddConsumedAsync(session.Id, _beerId);
        Assert.Equal(6.5m, consumed.Price);

        await _sessions.DeleteConsumedAsync(consumed.Id);

        Assert.Empty(await _sessions.GetConsumedAsync(session.Id));
    }

    [Fact]
    public async Task RenameDuringEnd_DoesNotReopenSession()
    {
        var session = await _sessions.StartSessionAsync(_casinoId, "x");
        var end = session.Starttime + 300;
        await _sessions.EndSessionAsync(session.Id, end);

        await _sessions.UpdateSessionNameAsync(session.Id, "renamed after end");

        var stored = await _sessions.GetAsync(session.Id);
        Assert.Equal("renamed after end", stored!.Name);
        Assert.Equal(end, stored.Endtime);
    }

    [Fact]
    public async Task Casino_CannotBeDeleted_WhileReferencedBySessions()
    {
        await _sessions.StartSessionAsync(_casinoId, "x");

        Assert.False(await _casinos.DeleteAsync(_casinoId));
        Assert.NotNull(await _casinos.GetAsync(_casinoId));
    }
}
