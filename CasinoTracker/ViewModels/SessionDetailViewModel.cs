using System.Collections.ObjectModel;
using CasinoTracker.Controls;
using CasinoTracker.Helpers;
using CasinoTracker.Models;
using CasinoTracker.Services;
using CasinoTracker.ViewModels.Items;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace CasinoTracker.ViewModels;

public partial class SessionDetailViewModel : BaseViewModel, IQueryAttributable
{
    private static readonly Color PrimaryColor = Color.FromArgb("#2E7D32");
    private static readonly Color AccentColor = Color.FromArgb("#F9A825");
    private static readonly Color MoneyColor = Color.FromArgb("#1976D2");
    private static readonly Color FoodColor = Color.FromArgb("#8D6E63");

    private readonly ISessionService _sessions;
    private readonly IDialogService _dialogs;
    private readonly INavigationService _navigation;
    private bool _loaded;
    private AppTheme _populatedTheme;

    public SessionDetailViewModel(ISessionService sessions, IDialogService dialogs, INavigationService navigation)
    {
        _sessions = sessions;
        _dialogs = dialogs;
        _navigation = navigation;
        Title = "Session";
    }

    [ObservableProperty] private int _sessionId;
    [ObservableProperty] private string _name = string.Empty;
    [ObservableProperty] private string _casinoName = string.Empty;
    [ObservableProperty] private string _periodText = string.Empty;
    [ObservableProperty] private string _durationText = string.Empty;
    [ObservableProperty] private bool _isRunning;

    [ObservableProperty] private string _buyInsText = string.Empty;
    [ObservableProperty] private string _cashOutsText = string.Empty;
    [ObservableProperty] private decimal _gameResult;
    [ObservableProperty] private string _gameResultText = string.Empty;
    [ObservableProperty] private string _consumptionText = string.Empty;
    [ObservableProperty] private string _consumedCountText = string.Empty;
    [ObservableProperty] private string _finalBankrollText = string.Empty;
    [ObservableProperty] private string _finalMoneyText = string.Empty;
    [ObservableProperty] private string _finalChipsText = string.Empty;

    [ObservableProperty] private List<ChartSeries> _bankrollSeries = new();
    [ObservableProperty] private List<ChartSeries> _exchangeSeries = new();
    [ObservableProperty] private List<ChartSeries> _consumptionSeries = new();
    [ObservableProperty] private List<BarItem> _consumedBars = new();

    [ObservableProperty] private bool _hasExchanges;
    [ObservableProperty] private bool _hasConsumed;
    [ObservableProperty] private bool _hasPlayedGames;

    public ObservableCollection<ExchangeItem> Exchanges { get; } = new();
    public ObservableCollection<ConsumedItem> Consumed { get; } = new();
    public ObservableCollection<PlayedGameItem> PlayedGames { get; } = new();

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue("id", out var value))
            SessionId = Convert.ToInt32(value);
    }

    private static AppTheme CurrentTheme => Application.Current?.RequestedTheme ?? AppTheme.Light;

    /// <summary>
    /// Invoked by the page when it appears. Loads once per page instance, and again when the
    /// theme changed meanwhile so the sign colours (chosen at conversion time) are refreshed.
    /// </summary>
    [RelayCommand]
    private async Task LoadAsync()
    {
        if (SessionId == 0) return;
        if (_loaded && _populatedTheme == CurrentTheme) return;
        _loaded = true;
        IsBusy = true;
        try
        {
            var detail = await _sessions.GetDetailAsync(SessionId);
            if (detail is null)
            {
                await _dialogs.AlertAsync("Not found", "This session does not exist anymore.");
                await _navigation.GoBackAsync();
                return;
            }

            Populate(detail);
        }
        catch (Exception ex)
        {
            await _dialogs.AlertAsync("Error", ex.Message);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private Task ReloadAsync()
    {
        _loaded = false;
        return LoadAsync();
    }

    private void Populate(SessionDetail d)
    {
        _populatedTheme = CurrentTheme;
        var s = d.Session;
        var start = s.Starttime;
        var end = s.Endtime ?? TimeHelper.Now();

        Name = string.IsNullOrWhiteSpace(s.Name) ? $"Session #{s.Id}" : s.Name;
        Title = Name;
        CasinoName = d.Casino?.Name ?? "Unknown casino";
        IsRunning = s.Endtime is null;
        PeriodText = s.Endtime is null
            ? $"{TimeHelper.FormatDateTime(start)} – running"
            : $"{TimeHelper.FormatDateTime(start)} – {TimeHelper.FormatTime(s.Endtime.Value)}";
        DurationText = TimeHelper.FormatDuration(TimeHelper.Duration(start, s.Endtime));

        BuyInsText = MoneyHelper.Format(d.BuyIns);
        CashOutsText = MoneyHelper.Format(d.CashOuts);
        GameResult = d.GameResult;
        GameResultText = MoneyHelper.FormatSigned(d.GameResult);
        ConsumptionText = MoneyHelper.Format(d.ConsumptionSpend);
        ConsumedCountText = d.Consumed.Count == 1 ? "1 item" : $"{d.Consumed.Count} items";

        var snapshot = BankrollCalculator.Compute(d.Exchanges, d.PlayedGames);
        FinalBankrollText = MoneyHelper.Format(snapshot.Bankroll);
        FinalMoneyText = MoneyHelper.Format(snapshot.Money);
        FinalChipsText = MoneyHelper.Format(snapshot.Chips);

        // ---- bankroll over time (step chart of CH / MO / BR, bankroll drawn last so it stays visible)
        var timeline = BankrollCalculator.Timeline(d.Exchanges, d.PlayedGames);
        var br = new List<ChartPoint> { new(start, 0) };
        var ch = new List<ChartPoint> { new(start, 0) };
        var mo = new List<ChartPoint> { new(start, 0) };
        foreach (var p in timeline)
        {
            br.Add(new ChartPoint(p.Timestamp, (double)p.Bankroll, p.Label));
            ch.Add(new ChartPoint(p.Timestamp, (double)p.Chips));
            mo.Add(new ChartPoint(p.Timestamp, (double)p.Money));
        }
        if (timeline.Count > 0 && end > timeline[^1].Timestamp)
        {
            var last = timeline[^1];
            br.Add(new ChartPoint(end, (double)last.Bankroll));
            ch.Add(new ChartPoint(end, (double)last.Chips));
            mo.Add(new ChartPoint(end, (double)last.Money));
        }
        BankrollSeries = timeline.Count == 0
            ? new List<ChartSeries>()
            : new List<ChartSeries>
            {
                new() { Name = "Chips", Color = AccentColor, Kind = SeriesKind.Step, StrokeSize = 2f, Points = ch },
                new() { Name = "Money", Color = MoneyColor, Kind = SeriesKind.Step, StrokeSize = 2f, Points = mo },
                new() { Name = "Bankroll", Color = PrimaryColor, Kind = SeriesKind.Step, StrokeSize = 3.5f, Points = br, Fill = true },
            };

        // ---- exchanges as bars over time
        ExchangeSeries = d.Exchanges.Count == 0
            ? new List<ChartSeries>()
            : new List<ChartSeries>
            {
                new()
                {
                    Name = "Buy-in (+) / Cash-out (−)",
                    Color = PrimaryColor,
                    Kind = SeriesKind.Bars,
                    Points = d.Exchanges.Select(e => new ChartPoint(e.Timestamp, (double)e.Amount)).ToList(),
                },
            };

        // ---- consumption over time (cumulative count with item labels)
        var consumptionPoints = new List<ChartPoint> { new(start, 0) };
        var count = 0;
        foreach (var c in d.Consumed.OrderBy(c => c.Timestamp))
            consumptionPoints.Add(new ChartPoint(c.Timestamp, ++count, d.MenuitemName(c.MenuitemId)));
        if (count > 0 && end > d.Consumed[^1].Timestamp)
            consumptionPoints.Add(new ChartPoint(end, count));
        ConsumptionSeries = count == 0
            ? new List<ChartSeries>()
            : new List<ChartSeries>
            {
                new()
                {
                    Name = "Items consumed",
                    Color = FoodColor,
                    Kind = SeriesKind.Step,
                    ShowMarkers = true,
                    ShowLabels = true,
                    Points = consumptionPoints,
                },
            };

        ConsumedBars = d.Consumed
            .GroupBy(c => c.MenuitemId)
            .Select(g => new BarItem
            {
                Label = d.MenuitemName(g.Key),
                Value = g.Count(),
                Color = d.Menuitems.TryGetValue(g.Key, out var m) && m.Beverage ? AccentColor : FoodColor,
            })
            .OrderByDescending(b => b.Value)
            .ToList();

        // ---- lists
        Exchanges.Clear();
        foreach (var e in d.Exchanges.OrderBy(e => e.Timestamp)) Exchanges.Add(new ExchangeItem(e));
        HasExchanges = Exchanges.Count > 0;

        Consumed.Clear();
        foreach (var c in d.Consumed.OrderBy(c => c.Timestamp))
        {
            var item = d.Menuitems.TryGetValue(c.MenuitemId, out var m) ? m : null;
            Consumed.Add(new ConsumedItem(c, item?.Name ?? "Unknown item", item?.Beverage ?? false, d.PriceOf(c)));
        }
        HasConsumed = Consumed.Count > 0;

        PlayedGames.Clear();
        foreach (var g in d.PlayedGames.OrderBy(g => g.Starttime)) PlayedGames.Add(new PlayedGameItem(g, d.GameName(g.GameId)));
        HasPlayedGames = PlayedGames.Count > 0;
    }

    // ------------------------------------------------------------------ toolbar

    /// <summary>Runs a command body and turns any failure into a dialog instead of crashing the app.</summary>
    private async Task RunAsync(Func<Task> action)
    {
        try
        {
            await action();
        }
        catch (Exception ex)
        {
            await _dialogs.AlertAsync("Error", ex.Message);
        }
    }

    [RelayCommand]
    private Task RenameAsync() => RunAsync(async () =>
    {
        var newName = await _dialogs.PromptAsync("Rename session", "New name", Name, "Session name");
        if (string.IsNullOrWhiteSpace(newName)) return;
        await _sessions.UpdateSessionNameAsync(SessionId, newName);
        Name = newName.Trim();
        Title = Name;
    });

    [RelayCommand]
    private Task DeleteAsync() => RunAsync(async () =>
    {
        var confirmed = await _dialogs.ConfirmAsync(
            "Delete session?",
            $"\"{Name}\" including all exchanges, games and consumed items will be deleted permanently.",
            "Delete", "Cancel");
        if (!confirmed) return;

        await _sessions.DeleteSessionAsync(SessionId);
        await _navigation.GoBackAsync();
    });

    // ------------------------------------------------------------------ row corrections

    [RelayCommand]
    private async Task ExchangeActionsAsync(ExchangeItem? item)
    {
        if (item is null) return;
        try
        {
            const string edit = "Edit amount";
            const string delete = "Delete";
            var choice = await _dialogs.ActionSheetAsync($"{item.Description} · {item.AmountText} ({item.TimeText})", "Cancel", delete, edit);

            if (choice == edit)
            {
                var text = await _dialogs.PromptAsync("Edit amount",
                    item.Amount >= 0 ? "New amount exchanged into chips" : "New amount cashed out",
                    MoneyHelper.Format(Math.Abs(item.Amount)), "Amount", Keyboard.Numeric);
                if (text is null) return;
                if (!MoneyHelper.TryParse(text, out var amount) || amount <= 0)
                {
                    await _dialogs.AlertAsync("Invalid amount", "Please enter a positive amount.");
                    return;
                }
                await _sessions.UpdateExchangeAmountAsync(item.Id, item.Amount >= 0 ? amount : -amount);
            }
            else if (choice == delete)
            {
                var ok = await _dialogs.ConfirmAsync("Delete exchange?",
                    $"{item.Description} {item.AmountText} at {item.TimeText} will be removed.", "Delete", "Cancel");
                if (!ok) return;
                await _sessions.DeleteExchangeAsync(item.Id);
            }
            else
            {
                return;
            }

            await ReloadAsync();
        }
        catch (Exception ex)
        {
            await _dialogs.AlertAsync("Error", ex.Message);
        }
    }

    [RelayCommand]
    private async Task PlayedGameActionsAsync(PlayedGameItem? item)
    {
        if (item is null) return;
        try
        {
            if (item.IsRunning)
            {
                await _dialogs.AlertAsync("Game running", "This game is still running. Stop it on the Session tab first.");
                return;
            }

            const string edit = "Edit result";
            const string delete = "Delete";
            var choice = await _dialogs.ActionSheetAsync($"{item.Name} · {item.AmountText} ({item.TimeText})", "Cancel", delete, edit);

            if (choice == edit)
            {
                var text = await _dialogs.PromptAsync("Edit result", "Won (+) or lost (−) amount",
                    MoneyHelper.FormatSigned(item.Amount), "e.g. -50 or 120", Keyboard.Numeric);
                if (text is null) return;
                if (!MoneyHelper.TryParse(text, out var amount))
                {
                    await _dialogs.AlertAsync("Invalid amount", "Please enter a valid number.");
                    return;
                }
                await _sessions.UpdatePlayedGameAmountAsync(item.Id, amount);
            }
            else if (choice == delete)
            {
                var ok = await _dialogs.ConfirmAsync("Delete game?",
                    $"{item.Name} ({item.AmountText}) will be removed from this session.", "Delete", "Cancel");
                if (!ok) return;
                await _sessions.DeletePlayedGameAsync(item.Id);
            }
            else
            {
                return;
            }

            await ReloadAsync();
        }
        catch (Exception ex)
        {
            await _dialogs.AlertAsync("Error", ex.Message);
        }
    }

    [RelayCommand]
    private async Task ConsumedActionsAsync(ConsumedItem? item)
    {
        if (item is null) return;
        try
        {
            var ok = await _dialogs.ConfirmAsync("Remove item?",
                $"{item.Name} at {item.TimeText} ({item.PriceText}) will be removed from this session.", "Remove", "Cancel");
            if (!ok) return;

            await _sessions.DeleteConsumedAsync(item.Id);
            await ReloadAsync();
        }
        catch (Exception ex)
        {
            await _dialogs.AlertAsync("Error", ex.Message);
        }
    }
}
