namespace CasinoTracker.Models;

/// <summary>Everything that belongs to a single session (read model).</summary>
public sealed class SessionDetail
{
    public required Session Session { get; init; }
    public Casino? Casino { get; init; }
    public required List<Exchange> Exchanges { get; init; }
    public required List<PlayedGame> PlayedGames { get; init; }
    public required List<ConsumedMenuitem> Consumed { get; init; }
    public required Dictionary<int, Menuitem> Menuitems { get; init; }
    public required Dictionary<int, Game> Games { get; init; }

    /// <summary>Price per menu item id at the casino of this session.</summary>
    public required Dictionary<int, decimal> Prices { get; init; }

    /// <summary>Current menu price of an item at this casino (0 when it is not on the menu).</summary>
    public decimal PriceOf(int menuitemId) => Prices.TryGetValue(menuitemId, out var p) ? p : 0m;

    /// <summary>Price of a consumed row: the snapshot taken at consumption time, else the current menu price.</summary>
    public decimal PriceOf(ConsumedMenuitem consumed) => consumed.Price ?? PriceOf(consumed.MenuitemId);

    public string MenuitemName(int id) => Menuitems.TryGetValue(id, out var m) ? m.Name : "Unknown item";
    public string GameName(int id) => Games.TryGetValue(id, out var g) ? g.Name : "Unknown game";

    public decimal ConsumptionSpend => Consumed.Sum(PriceOf);
    public decimal GameResult => PlayedGames.Sum(g => g.Amount);
    public decimal BuyIns => Exchanges.Where(e => e.Amount > 0).Sum(e => e.Amount);
    public decimal CashOuts => -Exchanges.Where(e => e.Amount < 0).Sum(e => e.Amount);
}
