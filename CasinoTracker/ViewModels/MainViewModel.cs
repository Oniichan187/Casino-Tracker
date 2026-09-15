using System.Collections.ObjectModel;
using CasinoTracker.Helpers;
using CasinoTracker.Models;
using CasinoTracker.Services;
using CasinoTracker.ViewModels.Items;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace CasinoTracker.ViewModels;

public partial class MainViewModel : BaseViewModel
{
    public const string ModeLost = "lost";
    public const string ModeWon = "won";
    public const string ModeChipsLeft = "left";

    private readonly ISessionService _sessions;
    private readonly ICasinoService _casinos;
    private readonly IMenuitemService _menuitems;
    private readonly ISettingsService _settings;
    private readonly IDialogService _dialogs;
    private readonly INavigationService _navigation;
    private readonly IDispatcherTimer _timer;

    private Session? _session;
    private PlayedGame? _currentGame;
    private BankrollSnapshot _bankroll;
    private CancellationTokenSource? _nameSaveCts;
    private string? _pendingName;
    private bool _suppressNameSave;
    private bool _autoClosing;
    private bool _isVisible;

    public MainViewModel(
        ISessionService sessions,
        ICasinoService casinos,
        IMenuitemService menuitems,
        ISettingsService settings,
        IDialogService dialogs,
        INavigationService navigation,
        IDispatcher dispatcher)
    {
        _sessions = sessions;
        _casinos = casinos;
        _menuitems = menuitems;
        _settings = settings;
        _dialogs = dialogs;
        _navigation = navigation;

        Title = "Session";
        UpdateStopHint();

        _timer = dispatcher.CreateTimer();
        _timer.Interval = TimeSpan.FromSeconds(1);
        _timer.Tick += (_, _) => OnTick();
    }

    // ------------------------------------------------------------------ no-session state

    [ObservableProperty] private bool _hasActiveSession;
    [ObservableProperty] private bool _hasCasinos;
    [ObservableProperty] private Casino? _selectedCasino;
    [ObservableProperty] private string _newSessionName = string.Empty;

    public ObservableCollection<Casino> Casinos { get; } = new();

    // ------------------------------------------------------------------ active session state

    [ObservableProperty] private string _sessionName = string.Empty;
    [ObservableProperty] private string _casinoName = string.Empty;
    [ObservableProperty] private string _sessionStartText = string.Empty;
    [ObservableProperty] private string _elapsedText = "00:00:00";
    [ObservableProperty] private string _autoStopText = string.Empty;

    [ObservableProperty] private string _bankrollText = "0.00";
    [ObservableProperty] private string _moneyText = "0.00";
    [ObservableProperty] private string _chipsText = "0.00";

    [ObservableProperty] private string _exchangeAmountText = string.Empty;
    public ObservableCollection<ExchangeItem> Exchanges { get; } = new();

    [ObservableProperty] private bool _hasGames;
    [ObservableProperty] private Game? _selectedGame;
    [ObservableProperty] private bool _isPlaying;
    [ObservableProperty] private string _currentGameName = string.Empty;
    [ObservableProperty] private string _gameElapsedText = "00:00:00";
    [ObservableProperty] private bool _isStopPanelVisible;
    [ObservableProperty] private string _stopMode = ModeLost;
    [ObservableProperty] private string _stopAmountText = string.Empty;
    [ObservableProperty] private string _stopHintText = string.Empty;
    [ObservableProperty] private string _stopAmountPlaceholder = "Amount lost";
    public ObservableCollection<Game> Games { get; } = new();
    public ObservableCollection<PlayedGameItem> PlayedGames { get; } = new();

    [ObservableProperty] private bool _hasMenu;
    [ObservableProperty] private MenuEntry? _selectedMenuEntry;
    [ObservableProperty] private string _consumptionTotalText = string.Empty;
    public ObservableCollection<MenuEntry> Menu { get; } = new();
    public ObservableCollection<ConsumedGroup> Consumed { get; } = new();

    // ------------------------------------------------------------------ page lifecycle

    /// <summary>Called by the page when it becomes visible, before the load starts.</summary>
    public void OnPageAppearing() => _isVisible = true;

    /// <summary>Called by the page whenever it disappears (tab switch, navigation, window teardown).</summary>
    public void OnPageDisappearing()
    {
        _isVisible = false;
        _timer.Stop();
    }

    /// <summary>The clock only runs while the page is on screen, even if a load finished after leaving it.</summary>
    private void StartTimerIfVisible()
    {
        if (_isVisible) _timer.Start();
    }

    /// <summary>
    /// True when the given session is still the active one. Compared by id, because a reload
    /// replaces the cached instance with an equal but different object.
    /// </summary>
    private bool IsStillActive(Session session) => _session?.Id == session.Id;

    [RelayCommand]
    private async Task LoadAsync()
    {
        if (IsBusy) return;
        IsBusy = true;
        try
        {
            await FlushPendingNameSaveAsync();
            await _sessions.AutoCloseExpiredSessionsAsync(TimeSpan.FromHours(_settings.AutoStopHours));
            _session = await _sessions.GetActiveSessionAsync();

            if (_session is null)
            {
                await ShowNoSessionAsync();
                return;
            }

            await LoadSessionAsync();

            // An auto-close may have ended the session while the data was loading.
            if (_session is null)
            {
                await ShowNoSessionAsync();
                return;
            }

            HasActiveSession = true;
            StartTimerIfVisible();
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

    private async Task ShowNoSessionAsync()
    {
        _timer.Stop();
        _session = null;
        _currentGame = null;
        IsPlaying = false;
        IsStopPanelVisible = false;
        HasActiveSession = false;
        await LoadCasinosAsync();
    }

    private async Task LoadCasinosAsync()
    {
        var selectedId = SelectedCasino?.Id;
        var casinos = await _casinos.GetAllAsync();

        Casinos.Clear();
        foreach (var c in casinos) Casinos.Add(c);

        HasCasinos = Casinos.Count > 0;
        SelectedCasino = Casinos.FirstOrDefault(c => c.Id == selectedId) ?? Casinos.FirstOrDefault();
    }

    private async Task LoadSessionAsync()
    {
        var session = _session;
        if (session is null) return;

        var casino = await _casinos.GetAsync(session.CasinoId);
        CasinoName = casino?.Name ?? "Unknown casino";
        SessionStartText = TimeHelper.FormatDateTime(session.Starttime);

        _suppressNameSave = true;
        SessionName = session.Name;
        _suppressNameSave = false;

        // Games available at this casino
        var selectedGameId = SelectedGame?.Id;
        var games = await _casinos.GetGamesAsync(session.CasinoId);
        Games.Clear();
        foreach (var g in games) Games.Add(g);
        HasGames = Games.Count > 0;
        SelectedGame = Games.FirstOrDefault(g => g.Id == selectedGameId) ?? Games.FirstOrDefault();

        // Menu available at this casino
        var selectedItemId = SelectedMenuEntry?.Item.Id;
        var menu = await _casinos.GetMenuAsync(session.CasinoId);
        Menu.Clear();
        foreach (var m in menu) Menu.Add(m);
        HasMenu = Menu.Count > 0;
        SelectedMenuEntry = Menu.FirstOrDefault(m => m.Item.Id == selectedItemId) ?? Menu.FirstOrDefault();

        // Running game - keep the stop panel open when the same game is still running
        var previousGameId = _currentGame?.Id;
        _currentGame = await _sessions.GetOpenPlayedGameAsync(session.Id);
        IsPlaying = _currentGame is not null;
        if (_currentGame is not null)
            CurrentGameName = Games.FirstOrDefault(g => g.Id == _currentGame.GameId)?.Name ?? "Game";
        if (_currentGame is null || _currentGame.Id != previousGameId)
            IsStopPanelVisible = false;

        await RefreshExchangesAsync();
        await RefreshPlayedGamesAsync();
        await RefreshConsumedAsync();
        await RefreshBankrollAsync();
        UpdateClock();
    }

    private async Task RefreshBankrollAsync()
    {
        var session = _session;
        if (session is null) return;
        _bankroll = await _sessions.GetBankrollAsync(session.Id);
        BankrollText = MoneyHelper.Format(_bankroll.Bankroll);
        MoneyText = MoneyHelper.Format(_bankroll.Money);
        ChipsText = MoneyHelper.Format(_bankroll.Chips);
        UpdateStopHint();
    }

    private async Task RefreshExchangesAsync()
    {
        var session = _session;
        if (session is null) return;
        var exchanges = await _sessions.GetExchangesAsync(session.Id);
        Exchanges.Clear();
        foreach (var e in exchanges.OrderByDescending(e => e.Timestamp).ThenByDescending(e => e.Id))
            Exchanges.Add(new ExchangeItem(e));
    }

    private async Task RefreshPlayedGamesAsync()
    {
        var session = _session;
        if (session is null) return;
        var played = await _sessions.GetPlayedGamesAsync(session.Id);
        PlayedGames.Clear();
        foreach (var p in played.OrderByDescending(p => p.Starttime).ThenByDescending(p => p.Id))
            PlayedGames.Add(new PlayedGameItem(p, Games.FirstOrDefault(g => g.Id == p.GameId)?.Name ?? "Game"));
    }

    private async Task RefreshConsumedAsync()
    {
        var session = _session;
        if (session is null) return;
        var consumed = await _sessions.GetConsumedAsync(session.Id);

        var groups = new List<ConsumedGroup>();
        foreach (var group in consumed.GroupBy(c => c.MenuitemId))
        {
            var entry = Menu.FirstOrDefault(m => m.Item.Id == group.Key);
            var item = entry?.Item ?? await _menuitems.GetAsync(group.Key);
            var rows = group.ToList();

            // Snapshot price per row, falling back to the current menu price for legacy rows.
            var total = rows.Sum(c => c.Price ?? entry?.Price ?? 0m);
            var unit = rows.Count > 0 ? total / rows.Count : entry?.Price ?? 0m;

            groups.Add(new ConsumedGroup(
                group.Key,
                item?.Name ?? "Unknown item",
                item?.Beverage ?? false,
                rows.Count,
                unit,
                total));
        }

        Consumed.Clear();
        foreach (var g in groups.OrderBy(g => g.IsBeverage).ThenBy(g => g.Name, StringComparer.CurrentCultureIgnoreCase))
            Consumed.Add(g);

        var totalSpend = groups.Sum(g => g.Total);
        var count = groups.Sum(g => g.Count);
        ConsumptionTotalText = count switch
        {
            0 => string.Empty,
            1 => $"1 item · {MoneyHelper.Format(totalSpend)}",
            _ => $"{count} items · {MoneyHelper.Format(totalSpend)}",
        };
    }

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

    // ------------------------------------------------------------------ session lifecycle

    [RelayCommand]
    private Task StartSessionAsync() => RunAsync(async () =>
    {
        if (SelectedCasino is null)
        {
            await _dialogs.AlertAsync("No casino", "Please select a casino first.");
            return;
        }

        _session = await _sessions.StartSessionAsync(SelectedCasino.Id, NewSessionName);
        NewSessionName = string.Empty;
        await LoadSessionAsync();
        HasActiveSession = true;
        StartTimerIfVisible();
    });

    [RelayCommand]
    private Task EndSessionAsync() => RunAsync(async () =>
    {
        var session = _session;
        if (session is null) return;

        if (IsPlaying)
        {
            await _dialogs.AlertAsync("Game running", "Please stop the running game before ending the session.");
            return;
        }

        var confirmed = await _dialogs.ConfirmAsync(
            "End session?",
            $"Do you really want to end \"{SessionName}\"?\n\nFinal bankroll: {BankrollText}\nMoney: {MoneyText} · Chips: {ChipsText}",
            "End session",
            "Keep playing");
        if (!confirmed || !IsStillActive(session)) return; // auto-closed meanwhile

        await FlushPendingNameSaveAsync();
        await _sessions.EndSessionAsync(session.Id);
        await ShowNoSessionAsync();
    });

    [RelayCommand]
    private Task GoToSettingsAsync() => _navigation.GoToAsync("//settings");

    // ------------------------------------------------------------------ session name (debounced save)

    partial void OnSessionNameChanged(string value)
    {
        if (_suppressNameSave || _session is null) return;

        _pendingName = value;
        _nameSaveCts?.Cancel();
        var cts = _nameSaveCts = new CancellationTokenSource();
        _ = SaveNameDebouncedAsync(_session.Id, value, cts.Token);
    }

    private async Task SaveNameDebouncedAsync(int sessionId, string name, CancellationToken token)
    {
        try
        {
            await Task.Delay(500, token);
            await _sessions.UpdateSessionNameAsync(sessionId, name);
            if (_session is { } s && s.Id == sessionId) s.Name = name.Trim();
            if (_pendingName == name) _pendingName = null;
        }
        catch (OperationCanceledException)
        {
            // superseded by a newer edit or flushed explicitly
        }
        catch (Exception ex)
        {
            await _dialogs.AlertAsync("Could not save the session name", ex.Message);
        }
    }

    /// <summary>Writes a not-yet-saved name immediately so a reload or session end never loses it.</summary>
    private async Task FlushPendingNameSaveAsync()
    {
        var session = _session;
        var name = _pendingName;
        if (session is null || name is null) return;

        _nameSaveCts?.Cancel();
        _nameSaveCts = null;

        // The name stays pending until it is actually stored, so a failed write is not silently lost.
        await _sessions.UpdateSessionNameAsync(session.Id, name);
        session.Name = name.Trim();
        if (_pendingName == name) _pendingName = null;
    }

    // ------------------------------------------------------------------ exchanges

    [RelayCommand]
    private Task BuyInAsync() => RunAsync(() => AddExchangeAsync(+1));

    [RelayCommand]
    private Task CashOutAsync() => RunAsync(() => AddExchangeAsync(-1));

    private async Task AddExchangeAsync(int sign)
    {
        var session = _session;
        if (session is null) return;

        if (!MoneyHelper.TryParse(ExchangeAmountText, out var amount) || amount <= 0)
        {
            await _dialogs.AlertAsync("Invalid amount", "Please enter a positive amount.");
            return;
        }

        if (sign < 0 && amount > _bankroll.Chips)
        {
            var ok = await _dialogs.ConfirmAsync(
                "More than your chips",
                $"You currently have {ChipsText} in chips but want to cash out {MoneyHelper.Format(amount)}. Continue anyway?",
                "Cash out", "Cancel");
            if (!ok || !IsStillActive(session)) return;
        }

        await _sessions.AddExchangeAsync(session.Id, sign * amount);
        ExchangeAmountText = string.Empty;
        await RefreshExchangesAsync();
        await RefreshBankrollAsync();
    }

    /// <summary>Tap on an exchange row: edit the amount or delete the entry.</summary>
    [RelayCommand]
    private Task ExchangeActionsAsync(ExchangeItem? item) => RunAsync(async () =>
    {
        var session = _session;
        if (session is null || item is null) return;

        const string edit = "Edit amount";
        const string delete = "Delete";
        var choice = await _dialogs.ActionSheetAsync($"{item.Description} · {item.AmountText} ({item.TimeText})", "Cancel", delete, edit);
        if (!IsStillActive(session)) return;

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

        await RefreshExchangesAsync();
        await RefreshBankrollAsync();
    });

    // ------------------------------------------------------------------ games

    [RelayCommand]
    private Task StartGameAsync() => RunAsync(async () =>
    {
        var session = _session;
        if (session is null) return;
        if (SelectedGame is null)
        {
            await _dialogs.AlertAsync("No game", "Please select a game first.");
            return;
        }

        _currentGame = await _sessions.StartGameAsync(session.Id, SelectedGame.Id);
        CurrentGameName = SelectedGame.Name;
        GameElapsedText = "00:00:00";
        IsPlaying = true;
        IsStopPanelVisible = false;
        await RefreshPlayedGamesAsync();
    });

    [RelayCommand]
    private void RequestStopGame()
    {
        StopAmountText = string.Empty;
        UpdateStopHint();
        IsStopPanelVisible = true;
    }

    [RelayCommand]
    private void CancelStopGame() => IsStopPanelVisible = false;

    [RelayCommand]
    private Task ConfirmStopGameAsync() => RunAsync(async () =>
    {
        var game = _currentGame;
        if (game is null) return;

        if (!MoneyHelper.TryParse(StopAmountText, out var value))
        {
            await _dialogs.AlertAsync("Invalid amount", "Please enter a valid number.");
            return;
        }

        var delta = StopMode switch
        {
            ModeWon => Math.Abs(value),
            ModeChipsLeft => value - _bankroll.Chips,
            _ => -Math.Abs(value),
        };

        await _sessions.StopGameAsync(game.Id, delta);
        if (_currentGame?.Id == game.Id)
        {
            _currentGame = null;
            IsPlaying = false;
        }
        IsStopPanelVisible = false;
        StopAmountText = string.Empty;

        await RefreshPlayedGamesAsync();
        await RefreshBankrollAsync();
    });

    /// <summary>Tap on a played-game row: edit the result, delete it, or discard a running game.</summary>
    [RelayCommand]
    private Task PlayedGameActionsAsync(PlayedGameItem? item) => RunAsync(async () =>
    {
        var session = _session;
        if (session is null || item is null) return;

        if (item.IsRunning)
        {
            var discard = await _dialogs.ConfirmAsync("Discard running game?",
                $"{item.Name} is still running. Discard it without booking a result?", "Discard", "Keep playing");
            if (!discard || !IsStillActive(session)) return;

            await _sessions.DeletePlayedGameAsync(item.Id);
            if (_currentGame?.Id == item.Id)
            {
                _currentGame = null;
                IsPlaying = false;
                IsStopPanelVisible = false;
            }
        }
        else
        {
            const string edit = "Edit result";
            const string delete = "Delete";
            var choice = await _dialogs.ActionSheetAsync($"{item.Name} · {item.AmountText} ({item.TimeText})", "Cancel", delete, edit);
            if (!IsStillActive(session)) return;

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
        }

        await RefreshPlayedGamesAsync();
        await RefreshBankrollAsync();
    });

    partial void OnStopModeChanged(string value) => UpdateStopHint();

    private void UpdateStopHint()
    {
        switch (StopMode)
        {
            case ModeWon:
                StopAmountPlaceholder = "Amount won";
                StopHintText = "Enter how much you won in this game. It is added to your chips.";
                break;
            case ModeChipsLeft:
                StopAmountPlaceholder = "Chips left";
                StopHintText = $"Enter the chips you hold now. You currently have {MoneyHelper.Format(_bankroll.Chips)} " +
                               "(exchanges during the game included, this game's result not yet). The difference is booked as the result.";
                break;
            default:
                StopAmountPlaceholder = "Amount lost";
                StopHintText = "Enter how much you lost in this game. It is subtracted from your chips.";
                break;
        }
    }

    // ------------------------------------------------------------------ consumption

    [RelayCommand]
    private Task AddConsumedAsync() => RunAsync(async () =>
    {
        var session = _session;
        if (session is null) return;
        if (SelectedMenuEntry is null)
        {
            await _dialogs.AlertAsync("No item", "Please select a menu item first.");
            return;
        }

        await _sessions.AddConsumedAsync(session.Id, SelectedMenuEntry.Item.Id);
        await RefreshConsumedAsync();
    });

    [RelayCommand]
    private Task IncrementConsumedAsync(ConsumedGroup? group) => RunAsync(async () =>
    {
        var session = _session;
        if (session is null || group is null) return;
        await _sessions.AddConsumedAsync(session.Id, group.MenuitemId);
        await RefreshConsumedAsync();
    });

    [RelayCommand]
    private Task DecrementConsumedAsync(ConsumedGroup? group) => RunAsync(async () =>
    {
        var session = _session;
        if (session is null || group is null) return;
        await _sessions.RemoveLastConsumedAsync(session.Id, group.MenuitemId);
        await RefreshConsumedAsync();
    });

    // ------------------------------------------------------------------ timer

    private void OnTick()
    {
        if (IsBusy || _session is null) return;
        UpdateClock();

        var limitSeconds = _settings.AutoStopHours * 3600L;
        if (!_autoClosing && TimeHelper.Now() - _session.Starttime >= limitSeconds)
        {
            _autoClosing = true;
            _ = HandleAutoCloseAsync();
        }
    }

    private async Task HandleAutoCloseAsync()
    {
        var hours = _settings.AutoStopHours;
        try
        {
            _timer.Stop();
            await FlushPendingNameSaveAsync();
            await _sessions.AutoCloseExpiredSessionsAsync(TimeSpan.FromHours(hours));
            await ShowNoSessionAsync();
            await _dialogs.AlertAsync("Session ended", $"The session was stopped automatically after {hours} hours.");
        }
        catch (Exception ex)
        {
            // The timer stays stopped; the next page appearance or app resume retries the auto-close.
            await _dialogs.AlertAsync("Auto-stop failed", ex.Message);
        }
        finally
        {
            _autoClosing = false;
        }
    }

    private void UpdateClock()
    {
        var session = _session;
        if (session is null) return;

        var elapsed = TimeHelper.Duration(session.Starttime, null);
        ElapsedText = TimeHelper.FormatClock(elapsed);

        var remaining = TimeSpan.FromHours(_settings.AutoStopHours) - elapsed;
        AutoStopText = remaining > TimeSpan.Zero
            ? $"Auto-stop in {TimeHelper.FormatDuration(remaining)}"
            : "Auto-stop due";

        if (_currentGame is { } game)
            GameElapsedText = TimeHelper.FormatClock(TimeHelper.Duration(game.Starttime, null));
    }
}
