using System.Collections.ObjectModel;
using System.Globalization;
using CasinoTracker.Helpers;
using CasinoTracker.Models;
using CasinoTracker.Services;
using CasinoTracker.ViewModels.Items;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace CasinoTracker.ViewModels;

public partial class CasinoEditViewModel : BaseViewModel, IQueryAttributable
{
    private readonly ICasinoService _casinos;
    private readonly IGameService _games;
    private readonly IMenuitemService _menuitems;
    private readonly IDialogService _dialogs;
    private readonly INavigationService _navigation;

    public CasinoEditViewModel(
        ICasinoService casinos,
        IGameService games,
        IMenuitemService menuitems,
        IDialogService dialogs,
        INavigationService navigation)
    {
        _casinos = casinos;
        _games = games;
        _menuitems = menuitems;
        _dialogs = dialogs;
        _navigation = navigation;
        Title = "New casino";
    }

    [ObservableProperty] private int _id;
    [ObservableProperty] private bool _isNew = true;
    [ObservableProperty] private string _name = string.Empty;
    [ObservableProperty] private string _address = string.Empty;
    [ObservableProperty] private string _latitudeText = string.Empty;
    [ObservableProperty] private string _longitudeText = string.Empty;
    [ObservableProperty] private bool _isLocating;

    [ObservableProperty] private bool _hasGameOptions;
    [ObservableProperty] private bool _hasMenuOptions;

    public ObservableCollection<SelectableItem> GameOptions { get; } = new();
    public ObservableCollection<SelectableItem> MenuOptions { get; } = new();

    private bool _loaded;

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue("id", out var value))
            Id = Convert.ToInt32(value);
    }

    /// <summary>Invoked by the page when it appears. Loads only once so unsaved edits survive a resume.</summary>
    [RelayCommand]
    private async Task LoadAsync()
    {
        if (_loaded) return;
        _loaded = true;
        IsBusy = true;
        try
        {
            IsNew = Id == 0;
            Title = IsNew ? "New casino" : "Edit casino";

            var allGames = await _games.GetAllAsync();
            var allItems = await _menuitems.GetAllAsync();

            var selectedGames = new HashSet<int>();
            var prices = new Dictionary<int, decimal>();

            if (!IsNew)
            {
                var casino = await _casinos.GetAsync(Id);
                if (casino is null)
                {
                    await _dialogs.AlertAsync("Not found", "This casino does not exist anymore.");
                    await _navigation.GoBackAsync();
                    return;
                }

                Name = casino.Name;
                Address = casino.Address ?? string.Empty;
                LatitudeText = casino.Latitude?.ToString(CultureInfo.InvariantCulture) ?? string.Empty;
                LongitudeText = casino.Longitude?.ToString(CultureInfo.InvariantCulture) ?? string.Empty;

                selectedGames = (await _casinos.GetGameLinksAsync(Id)).Select(l => l.GameId).ToHashSet();
                prices = (await _casinos.GetMenuLinksAsync(Id)).GroupBy(l => l.MenuitemId).ToDictionary(g => g.Key, g => g.First().Price);
            }

            GameOptions.Clear();
            foreach (var g in allGames)
                GameOptions.Add(new SelectableItem(g.Id, g.Name, showPrice: false) { IsSelected = selectedGames.Contains(g.Id) });
            HasGameOptions = GameOptions.Count > 0;

            MenuOptions.Clear();
            foreach (var m in allItems)
            {
                var option = new SelectableItem(m.Id, m.Name, showPrice: true, m.Beverage ? "Beverage" : "Food");
                if (prices.TryGetValue(m.Id, out var price))
                {
                    option.IsSelected = true;
                    option.PriceText = MoneyHelper.Format(price);
                }
                MenuOptions.Add(option);
            }
            HasMenuOptions = MenuOptions.Count > 0;
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

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (string.IsNullOrWhiteSpace(Name))
        {
            await _dialogs.AlertAsync("Name missing", "Please enter a name for the casino.");
            return;
        }

        double? lat = null, lon = null;
        var hasLat = !string.IsNullOrWhiteSpace(LatitudeText);
        var hasLon = !string.IsNullOrWhiteSpace(LongitudeText);
        if (hasLat || hasLon)
        {
            if (!TryParseCoordinate(LatitudeText, -90, 90, out var la) || !TryParseCoordinate(LongitudeText, -180, 180, out var lo))
            {
                await _dialogs.AlertAsync("Invalid coordinates", "Please enter both latitude (-90..90) and longitude (-180..180), or leave both empty.");
                return;
            }
            lat = la;
            lon = lo;
        }

        var missingPrices = MenuOptions
            .Where(o => o.IsSelected && (!MoneyHelper.TryParse(o.PriceText, out var p) || p < 0))
            .Select(o => o.Name)
            .ToList();
        if (missingPrices.Count > 0)
        {
            await _dialogs.AlertAsync("Price missing",
                $"Please enter a price for: {string.Join(", ", missingPrices)}. Use 0 for complimentary items.");
            return;
        }

        var casino = new Casino
        {
            Id = Id,
            Name = Name.Trim(),
            Address = string.IsNullOrWhiteSpace(Address) ? null : Address.Trim(),
            Latitude = lat,
            Longitude = lon,
        };

        try
        {
            var id = await _casinos.SaveAsync(casino);
            await _casinos.SetGamesAsync(id, GameOptions.Where(o => o.IsSelected).Select(o => o.Id));
            await _casinos.SetMenuAsync(id, MenuOptions.Where(o => o.IsSelected).Select(o => (o.Id, o.ParsedPrice)));
            await _navigation.GoBackAsync();
        }
        catch (Exception ex)
        {
            await _dialogs.AlertAsync("Error", ex.Message);
        }
    }

    [RelayCommand]
    private async Task DeleteAsync()
    {
        if (IsNew) return;

        var sessions = await _casinos.CountSessionsAsync(Id);
        if (sessions > 0)
        {
            await _dialogs.AlertAsync("Cannot delete", $"This casino is used in {sessions} session(s). Delete those sessions first.");
            return;
        }

        var confirmed = await _dialogs.ConfirmAsync("Delete casino?", $"Delete \"{Name}\"?", "Delete", "Cancel");
        if (!confirmed) return;

        await _casinos.DeleteAsync(Id);
        await _navigation.GoBackAsync();
    }

    [RelayCommand]
    private async Task UseCurrentLocationAsync()
    {
        if (IsLocating) return;
        IsLocating = true;
        try
        {
            var status = await Permissions.CheckStatusAsync<Permissions.LocationWhenInUse>();
            if (status != PermissionStatus.Granted)
                status = await Permissions.RequestAsync<Permissions.LocationWhenInUse>();
            if (status != PermissionStatus.Granted)
            {
                await _dialogs.AlertAsync("No permission", "Location permission was not granted.");
                return;
            }

            var request = new GeolocationRequest(GeolocationAccuracy.Medium, TimeSpan.FromSeconds(15));
            var location = await Geolocation.Default.GetLocationAsync(request)
                           ?? await Geolocation.Default.GetLastKnownLocationAsync();
            if (location is null)
            {
                await _dialogs.AlertAsync("No location", "The current location could not be determined.");
                return;
            }

            LatitudeText = location.Latitude.ToString("F6", CultureInfo.InvariantCulture);
            LongitudeText = location.Longitude.ToString("F6", CultureInfo.InvariantCulture);
        }
        catch (FeatureNotSupportedException)
        {
            await _dialogs.AlertAsync("Not supported", "Location is not supported on this device.");
        }
        catch (FeatureNotEnabledException)
        {
            await _dialogs.AlertAsync("Location off", "Please enable location services.");
        }
        catch (Exception ex)
        {
            await _dialogs.AlertAsync("Error", ex.Message);
        }
        finally
        {
            IsLocating = false;
        }
    }

    private static bool TryParseCoordinate(string text, double min, double max, out double value)
    {
        value = 0;
        var normalised = text.Trim().Replace(',', '.');
        return double.TryParse(normalised, NumberStyles.Float, CultureInfo.InvariantCulture, out value)
               && value >= min && value <= max;
    }
}
