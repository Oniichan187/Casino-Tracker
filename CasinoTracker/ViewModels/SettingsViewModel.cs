using System.Collections.ObjectModel;
using CasinoTracker.Helpers;
using CasinoTracker.Services;
using CasinoTracker.ViewModels.Items;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace CasinoTracker.ViewModels;

public partial class SettingsViewModel : BaseViewModel
{
    private readonly ISettingsService _settings;
    private readonly ICasinoService _casinos;
    private readonly IMenuitemService _menuitems;
    private readonly IGameService _games;
    private readonly IDialogService _dialogs;
    private readonly INavigationService _navigation;
    private bool _initialising;

    public SettingsViewModel(
        ISettingsService settings,
        ICasinoService casinos,
        IMenuitemService menuitems,
        IGameService games,
        IDialogService dialogs,
        INavigationService navigation)
    {
        _settings = settings;
        _casinos = casinos;
        _menuitems = menuitems;
        _games = games;
        _dialogs = dialogs;
        _navigation = navigation;
        Title = "Settings";

        _initialising = true;
        IsDarkMode = settings.IsDarkMode;
        AutoStopHours = settings.AutoStopHours;
        _initialising = false;
        UpdateAutoStopText();
    }

    public int MinAutoStopHours => _settings.MinAutoStopHours;
    public int MaxAutoStopHours => _settings.MaxAutoStopHours;

    [ObservableProperty] private bool _isDarkMode;
    [ObservableProperty] private double _autoStopHours;
    [ObservableProperty] private string _autoStopText = string.Empty;

    [ObservableProperty] private bool _hasCasinos;
    [ObservableProperty] private bool _hasMenuitems;
    [ObservableProperty] private bool _hasGames;

    public ObservableCollection<ListEntry> Casinos { get; } = new();
    public ObservableCollection<ListEntry> Menuitems { get; } = new();
    public ObservableCollection<ListEntry> Games { get; } = new();

    partial void OnIsDarkModeChanged(bool value)
    {
        if (_initialising) return;
        _settings.IsDarkMode = value;
    }

    partial void OnAutoStopHoursChanged(double value)
    {
        var rounded = Math.Clamp((int)Math.Round(value), MinAutoStopHours, MaxAutoStopHours);
        if (Math.Abs(rounded - value) > 0.001)
        {
            AutoStopHours = rounded; // snap slider to whole hours
            return;
        }

        if (!_initialising)
            _settings.AutoStopHours = rounded;
        UpdateAutoStopText();
    }

    private void UpdateAutoStopText() =>
        AutoStopText = $"Sessions stop automatically after {(int)Math.Round(AutoStopHours)} hours";

    [RelayCommand]
    private async Task LoadAsync()
    {
        try
        {
            var casinos = await _casinos.GetAllAsync();
            Casinos.Clear();
            foreach (var c in casinos)
                Casinos.Add(new ListEntry(c.Id, c.Name, c.Address));
            HasCasinos = Casinos.Count > 0;

            var items = await _menuitems.GetAllAsync();
            Menuitems.Clear();
            foreach (var m in items)
                Menuitems.Add(new ListEntry(m.Id, m.Name, m.Beverage ? "Beverage" : "Food"));
            HasMenuitems = Menuitems.Count > 0;

            var games = await _games.GetAllAsync();
            Games.Clear();
            foreach (var g in games)
                Games.Add(new ListEntry(g.Id, g.Name));
            HasGames = Games.Count > 0;
        }
        catch (Exception ex)
        {
            await _dialogs.AlertAsync("Error", ex.Message);
        }
    }

    [RelayCommand]
    private Task AddCasinoAsync() => _navigation.GoToAsync("casinoedit");

    [RelayCommand]
    private Task EditCasinoAsync(ListEntry? entry) =>
        entry is null ? Task.CompletedTask : _navigation.GoToAsync("casinoedit", new Dictionary<string, object> { ["id"] = entry.Id });

    [RelayCommand]
    private Task AddMenuitemAsync() => _navigation.GoToAsync("menuitemedit");

    [RelayCommand]
    private Task EditMenuitemAsync(ListEntry? entry) =>
        entry is null ? Task.CompletedTask : _navigation.GoToAsync("menuitemedit", new Dictionary<string, object> { ["id"] = entry.Id });

    [RelayCommand]
    private Task AddGameAsync() => _navigation.GoToAsync("gameedit");

    [RelayCommand]
    private Task EditGameAsync(ListEntry? entry) =>
        entry is null ? Task.CompletedTask : _navigation.GoToAsync("gameedit", new Dictionary<string, object> { ["id"] = entry.Id });
}
