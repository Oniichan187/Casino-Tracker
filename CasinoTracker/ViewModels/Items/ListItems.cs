using CasinoTracker.Helpers;
using CasinoTracker.Models;
using CommunityToolkit.Mvvm.ComponentModel;

namespace CasinoTracker.ViewModels.Items;

/// <summary>Row for an exchange (buy-in / cash-out).</summary>
public sealed class ExchangeItem
{
    public ExchangeItem(Exchange exchange)
    {
        Id = exchange.Id;
        Amount = exchange.Amount;
        TimeText = TimeHelper.FormatTime(exchange.Timestamp);
        DateTimeText = TimeHelper.FormatDateTime(exchange.Timestamp);
        Description = exchange.Amount >= 0 ? "Money → Chips" : "Chips → Money";
        AmountText = MoneyHelper.FormatSigned(exchange.Amount);
    }

    public int Id { get; }
    public decimal Amount { get; }
    public string TimeText { get; }
    public string DateTimeText { get; }
    public string Description { get; }
    public string AmountText { get; }
}

/// <summary>Consumed menu items of one kind, summarised as "2x Beer".</summary>
public sealed class ConsumedGroup
{
    public ConsumedGroup(int menuitemId, string name, bool isBeverage, int count, decimal unitPrice, decimal total)
    {
        MenuitemId = menuitemId;
        Name = name;
        IsBeverage = isBeverage;
        Count = count;
        UnitPrice = unitPrice;
        Total = total;
    }

    public int MenuitemId { get; }
    public string Name { get; }
    public bool IsBeverage { get; }
    public int Count { get; }
    public decimal UnitPrice { get; }
    public decimal Total { get; }

    public string Text => $"{Count}x {Name}";
    public string Icon => IsBeverage ? "🍺" : "🍔";
    public string PriceText => $"{MoneyHelper.Format(UnitPrice)} each · {MoneyHelper.Format(Total)}";
}

/// <summary>A single consumed item with its timestamp (session detail list).</summary>
public sealed class ConsumedItem
{
    public ConsumedItem(ConsumedMenuitem consumed, string name, bool isBeverage, decimal price)
    {
        Id = consumed.Id;
        Name = name;
        IsBeverage = isBeverage;
        Price = price;
        TimeText = TimeHelper.FormatTime(consumed.Timestamp);
        PriceText = MoneyHelper.Format(price);
    }

    public int Id { get; }
    public string Name { get; }
    public bool IsBeverage { get; }
    public decimal Price { get; }
    public string TimeText { get; }
    public string PriceText { get; }
    public string Icon => IsBeverage ? "🍺" : "🍔";
}

/// <summary>Row for a played game.</summary>
public sealed class PlayedGameItem
{
    public PlayedGameItem(PlayedGame game, string name)
    {
        Id = game.Id;
        Name = name;
        Amount = game.Amount;
        IsRunning = game.Endtime is null;
        var end = game.Endtime;
        TimeText = end is null
            ? $"{TimeHelper.FormatTime(game.Starttime)} – running"
            : $"{TimeHelper.FormatTime(game.Starttime)} – {TimeHelper.FormatTime(end.Value)}";
        DurationText = TimeHelper.FormatDuration(TimeHelper.Duration(game.Starttime, end));
        AmountText = IsRunning ? "…" : MoneyHelper.FormatSigned(game.Amount);
    }

    public int Id { get; }
    public string Name { get; }
    public decimal Amount { get; }
    public bool IsRunning { get; }
    public string TimeText { get; }
    public string DurationText { get; }
    public string AmountText { get; }
}

/// <summary>Row for the sessions list.</summary>
public sealed class SessionListItem
{
    public SessionListItem(SessionSummary summary)
    {
        var s = summary.Session;
        Id = s.Id;
        Name = string.IsNullOrWhiteSpace(s.Name) ? $"Session #{s.Id}" : s.Name;
        CasinoName = summary.CasinoName;
        IsRunning = s.Endtime is null;
        DateText = TimeHelper.FormatDateTime(s.Starttime);
        DurationText = TimeHelper.FormatDuration(TimeHelper.Duration(s.Starttime, s.Endtime));
        Result = summary.GameResult;
        ResultText = MoneyHelper.FormatSigned(summary.GameResult);
        DetailsText = $"Buy-ins {MoneyHelper.Format(summary.BuyIns)} · Cash-outs {MoneyHelper.Format(summary.CashOuts)} · Food & drinks {MoneyHelper.Format(summary.ConsumptionSpend)}";
    }

    public int Id { get; }
    public string Name { get; }
    public string CasinoName { get; }
    public bool IsRunning { get; }
    public string DateText { get; }
    public string DurationText { get; }
    public decimal Result { get; }
    public string ResultText { get; }
    public string DetailsText { get; }
}

/// <summary>Generic row for the settings lists (casinos, menu items, games).</summary>
public sealed class ListEntry
{
    public ListEntry(int id, string name, string? subtitle = null)
    {
        Id = id;
        Name = name;
        Subtitle = subtitle ?? string.Empty;
    }

    public int Id { get; }
    public string Name { get; }
    public string Subtitle { get; }
    public bool HasSubtitle => !string.IsNullOrEmpty(Subtitle);
}

/// <summary>Checkbox row with optional price entry for the edit pages.</summary>
public sealed partial class SelectableItem : ObservableObject
{
    public SelectableItem(int id, string name, bool showPrice, string? subtitle = null)
    {
        Id = id;
        Name = name;
        ShowPrice = showPrice;
        Subtitle = subtitle ?? string.Empty;
    }

    public int Id { get; }
    public string Name { get; }
    public string Subtitle { get; }
    public bool HasSubtitle => !string.IsNullOrEmpty(Subtitle);
    public bool ShowPrice { get; }

    [ObservableProperty]
    private bool _isSelected;

    [ObservableProperty]
    private string _priceText = string.Empty;

    public decimal ParsedPrice => MoneyHelper.TryParse(PriceText, out var p) ? p : 0m;
}

/// <summary>Casino statistics row.</summary>
public sealed class CasinoStatItem
{
    public CasinoStatItem(CasinoStat stat)
    {
        Name = stat.Name;
        SessionsText = stat.Sessions == 1 ? "1 session" : $"{stat.Sessions} sessions";
        DurationText = TimeHelper.FormatDuration(stat.Duration);
        BuyInsText = MoneyHelper.Format(stat.BuyIns);
        CashOutsText = MoneyHelper.Format(stat.CashOuts);
        GameResult = stat.GameResult;
        GameResultText = MoneyHelper.FormatSigned(stat.GameResult);
        ConsumptionText = MoneyHelper.Format(stat.ConsumptionSpend);
        Balance = stat.Balance;
        BalanceText = MoneyHelper.FormatSigned(stat.Balance);
        Items = stat.Items.Select(i => new ItemStatRow(i)).ToList();
        HasItems = Items.Count > 0;
    }

    public string Name { get; }
    public string SessionsText { get; }
    public string DurationText { get; }
    public string BuyInsText { get; }
    public string CashOutsText { get; }
    public decimal GameResult { get; }
    public string GameResultText { get; }
    public string ConsumptionText { get; }
    public decimal Balance { get; }
    public string BalanceText { get; }
    public List<ItemStatRow> Items { get; }
    public bool HasItems { get; }
}

public sealed class ItemStatRow
{
    public ItemStatRow(ItemStat stat)
    {
        Text = $"{stat.Count}x {stat.Name}";
        SpendText = MoneyHelper.Format(stat.Spend);
        Icon = stat.IsBeverage ? "🍺" : "🍔";
    }

    public string Text { get; }
    public string SpendText { get; }
    public string Icon { get; }
}

public sealed class GameStatItem
{
    public GameStatItem(GameStat stat)
    {
        Name = stat.Name;
        TimesText = stat.TimesPlayed == 1 ? "played once" : $"played {stat.TimesPlayed}x";
        DurationText = TimeHelper.FormatDuration(stat.Duration);
        Result = stat.Result;
        ResultText = MoneyHelper.FormatSigned(stat.Result);
    }

    public string Name { get; }
    public string TimesText { get; }
    public string DurationText { get; }
    public decimal Result { get; }
    public string ResultText { get; }
}
