using CasinoTracker.Helpers;

namespace CasinoTracker.Models;

/// <summary>A menu item together with its price at a specific casino (read model, not a table).</summary>
public sealed class MenuEntry
{
    public MenuEntry(Menuitem item, decimal price)
    {
        Item = item;
        Price = price;
    }

    public Menuitem Item { get; }
    public decimal Price { get; }

    public string DisplayName => $"{Item.Name} · {MoneyHelper.Format(Price)}";
}
