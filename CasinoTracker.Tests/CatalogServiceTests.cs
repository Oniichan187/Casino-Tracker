using CasinoTracker.Models;
using CasinoTracker.Services;
using Xunit;

namespace CasinoTracker.Tests;

/// <summary>Casino / menu item / game services and their link tables.</summary>
public class CatalogServiceTests : IAsyncLifetime
{
    private readonly TestDatabase _db = new();
    private CasinoService _casinos = null!;
    private MenuitemService _menuitems = null!;
    private GameService _games = null!;

    public Task InitializeAsync()
    {
        _casinos = new CasinoService(_db);
        _menuitems = new MenuitemService(_db);
        _games = new GameService(_db);
        return Task.CompletedTask;
    }

    public async Task DisposeAsync() => await _db.DisposeAsync();

    [Fact]
    public async Task Save_InsertsThenUpdates()
    {
        var id = await _casinos.SaveAsync(new Casino { Name = "A", Address = "Street 1" });
        Assert.True(id > 0);

        var casino = await _casinos.GetAsync(id);
        casino!.Name = "B";
        casino.Latitude = 47.1;
        casino.Longitude = 8.2;
        Assert.Equal(id, await _casinos.SaveAsync(casino));

        var reloaded = await _casinos.GetAsync(id);
        Assert.Equal("B", reloaded!.Name);
        Assert.Equal(47.1, reloaded.Latitude);
        Assert.Single(await _casinos.GetAllAsync());
    }

    [Fact]
    public async Task SetMenu_ReplacesLinks_AndGetMenuJoinsPrices()
    {
        var casino = await _casinos.SaveAsync(new Casino { Name = "A" });
        var beer = await _menuitems.SaveAsync(new Menuitem { Name = "Beer", Beverage = true });
        var burger = await _menuitems.SaveAsync(new Menuitem { Name = "Burger", Beverage = false });
        var cola = await _menuitems.SaveAsync(new Menuitem { Name = "Cola", Beverage = true });

        await _casinos.SetMenuAsync(casino, new[] { (beer, 5m), (burger, 12m) });
        var menu = await _casinos.GetMenuAsync(casino);
        Assert.Equal(new[] { "Burger", "Beer" }, menu.Select(m => m.Item.Name).ToArray()); // food first, then by name
        Assert.Equal(12m, menu.Single(m => m.Item.Id == burger).Price);

        await _casinos.SetMenuAsync(casino, new[] { (beer, 6m), (cola, 4m) });
        menu = await _casinos.GetMenuAsync(casino);
        Assert.Equal(2, menu.Count);
        Assert.Equal(6m, menu.Single(m => m.Item.Id == beer).Price);
        Assert.DoesNotContain(menu, m => m.Item.Id == burger);

        // The same link table is visible from the menu item side
        var links = await _menuitems.GetCasinoLinksAsync(beer);
        Assert.Single(links);
        Assert.Equal(casino, links[0].CasinoId);
        Assert.Equal(6m, links[0].Price);
    }

    [Fact]
    public async Task SetGames_ReplacesLinks_BothDirections()
    {
        var a = await _casinos.SaveAsync(new Casino { Name = "A" });
        var b = await _casinos.SaveAsync(new Casino { Name = "B" });
        var roulette = await _games.SaveAsync(new Game { Name = "Roulette" });
        var poker = await _games.SaveAsync(new Game { Name = "Poker" });

        await _casinos.SetGamesAsync(a, new[] { roulette, poker, poker });
        Assert.Equal(new[] { "Poker", "Roulette" }, (await _casinos.GetGamesAsync(a)).Select(g => g.Name).ToArray());

        await _games.SetCasinosAsync(poker, new[] { b });
        Assert.Equal(new[] { b }, await _games.GetCasinoIdsAsync(poker));
        Assert.Equal(new[] { "Roulette" }, (await _casinos.GetGamesAsync(a)).Select(g => g.Name).ToArray());
        Assert.Equal(new[] { "Poker" }, (await _casinos.GetGamesAsync(b)).Select(g => g.Name).ToArray());
    }

    [Fact]
    public async Task MenuitemSetCasinos_WritesPricesPerCasino()
    {
        var a = await _casinos.SaveAsync(new Casino { Name = "A" });
        var b = await _casinos.SaveAsync(new Casino { Name = "B" });
        var beer = await _menuitems.SaveAsync(new Menuitem { Name = "Beer", Beverage = true });

        await _menuitems.SetCasinosAsync(beer, new[] { (a, 5m), (b, 7.5m) });

        Assert.Equal(5m, (await _casinos.GetMenuAsync(a)).Single().Price);
        Assert.Equal(7.5m, (await _casinos.GetMenuAsync(b)).Single().Price);
    }

    [Fact]
    public async Task DeleteCasino_RemovesLinks()
    {
        var casino = await _casinos.SaveAsync(new Casino { Name = "A" });
        var beer = await _menuitems.SaveAsync(new Menuitem { Name = "Beer", Beverage = true });
        var poker = await _games.SaveAsync(new Game { Name = "Poker" });
        await _casinos.SetMenuAsync(casino, new[] { (beer, 5m) });
        await _casinos.SetGamesAsync(casino, new[] { poker });

        Assert.True(await _casinos.DeleteAsync(casino));

        Assert.Null(await _casinos.GetAsync(casino));
        Assert.Empty(await _menuitems.GetCasinoLinksAsync(beer));
        Assert.Empty(await _games.GetCasinoIdsAsync(poker));
    }

    [Fact]
    public async Task DeleteMenuitemAndGame_BlockedWhenUsedInSessions()
    {
        var casino = await _casinos.SaveAsync(new Casino { Name = "A" });
        var beer = await _menuitems.SaveAsync(new Menuitem { Name = "Beer", Beverage = true });
        var poker = await _games.SaveAsync(new Game { Name = "Poker" });
        var sessions = new SessionService(_db);
        var session = await sessions.StartSessionAsync(casino, "s");
        await sessions.AddConsumedAsync(session.Id, beer);
        await sessions.StartGameAsync(session.Id, poker);

        Assert.False(await _menuitems.DeleteAsync(beer));
        Assert.False(await _games.DeleteAsync(poker));

        await sessions.DeleteSessionAsync(session.Id);

        Assert.True(await _menuitems.DeleteAsync(beer));
        Assert.True(await _games.DeleteAsync(poker));
        Assert.Empty(await _menuitems.GetAllAsync());
        Assert.Empty(await _games.GetAllAsync());
    }

    [Fact]
    public async Task Names_AreOrderedCaseInsensitively()
    {
        await _casinos.SaveAsync(new Casino { Name = "bellagio" });
        await _casinos.SaveAsync(new Casino { Name = "Zürich" });
        await _casinos.SaveAsync(new Casino { Name = "Aria" });
        await _games.SaveAsync(new Game { Name = "roulette" });
        await _games.SaveAsync(new Game { Name = "Blackjack" });

        Assert.Equal(new[] { "Aria", "bellagio", "Zürich" }, (await _casinos.GetAllAsync()).Select(c => c.Name).ToArray());
        Assert.Equal(new[] { "Blackjack", "roulette" }, (await _games.GetAllAsync()).Select(g => g.Name).ToArray());
    }

    [Fact]
    public async Task Menuitems_AreOrderedFoodFirstThenName()
    {
        await _menuitems.SaveAsync(new Menuitem { Name = "Wine", Beverage = true });
        await _menuitems.SaveAsync(new Menuitem { Name = "Steak", Beverage = false });
        await _menuitems.SaveAsync(new Menuitem { Name = "Beer", Beverage = true });

        var all = await _menuitems.GetAllAsync();
        Assert.Equal(new[] { "Steak", "Beer", "Wine" }, all.Select(m => m.Name).ToArray());
    }
}
