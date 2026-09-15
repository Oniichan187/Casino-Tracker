using System.Collections.ObjectModel;
using CasinoTracker.Helpers;
using CasinoTracker.Models;
using CasinoTracker.Services;
using CasinoTracker.ViewModels.Items;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace CasinoTracker.ViewModels;

public partial class MenuitemEditViewModel : BaseViewModel, IQueryAttributable
{
    private readonly IMenuitemService _menuitems;
    private readonly ICasinoService _casinos;
    private readonly IDialogService _dialogs;
    private readonly INavigationService _navigation;

    public MenuitemEditViewModel(
        IMenuitemService menuitems,
        ICasinoService casinos,
        IDialogService dialogs,
        INavigationService navigation)
    {
        _menuitems = menuitems;
        _casinos = casinos;
        _dialogs = dialogs;
        _navigation = navigation;
        Title = "New menu item";
    }

    [ObservableProperty] private int _id;
    [ObservableProperty] private bool _isNew = true;
    [ObservableProperty] private string _name = string.Empty;
    [ObservableProperty] private bool _isBeverage = true;
    [ObservableProperty] private bool _hasCasinoOptions;

    public ObservableCollection<SelectableItem> CasinoOptions { get; } = new();

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
            Title = IsNew ? "New menu item" : "Edit menu item";

            var casinos = await _casinos.GetAllAsync();
            var prices = new Dictionary<int, decimal>();

            if (!IsNew)
            {
                var item = await _menuitems.GetAsync(Id);
                if (item is null)
                {
                    await _dialogs.AlertAsync("Not found", "This menu item does not exist anymore.");
                    await _navigation.GoBackAsync();
                    return;
                }

                Name = item.Name;
                IsBeverage = item.Beverage;
                prices = (await _menuitems.GetCasinoLinksAsync(Id)).GroupBy(l => l.CasinoId).ToDictionary(g => g.Key, g => g.First().Price);
            }

            CasinoOptions.Clear();
            foreach (var c in casinos)
            {
                var option = new SelectableItem(c.Id, c.Name, showPrice: true, c.Address);
                if (prices.TryGetValue(c.Id, out var price))
                {
                    option.IsSelected = true;
                    option.PriceText = MoneyHelper.Format(price);
                }
                CasinoOptions.Add(option);
            }
            HasCasinoOptions = CasinoOptions.Count > 0;
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
            await _dialogs.AlertAsync("Name missing", "Please enter a name for the menu item.");
            return;
        }

        var missingPrices = CasinoOptions
            .Where(o => o.IsSelected && (!MoneyHelper.TryParse(o.PriceText, out var p) || p < 0))
            .Select(o => o.Name)
            .ToList();
        if (missingPrices.Count > 0)
        {
            await _dialogs.AlertAsync("Price missing",
                $"Please enter the price at: {string.Join(", ", missingPrices)}. Use 0 for complimentary items.");
            return;
        }

        try
        {
            var item = new Menuitem { Id = Id, Name = Name.Trim(), Beverage = IsBeverage };
            var id = await _menuitems.SaveAsync(item);
            await _menuitems.SetCasinosAsync(id, CasinoOptions.Where(o => o.IsSelected).Select(o => (o.Id, o.ParsedPrice)));
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

        var used = await _menuitems.CountConsumedAsync(Id);
        if (used > 0)
        {
            await _dialogs.AlertAsync("Cannot delete", $"This item was consumed {used} time(s) in sessions. Delete those sessions first.");
            return;
        }

        var confirmed = await _dialogs.ConfirmAsync("Delete menu item?", $"Delete \"{Name}\"?", "Delete", "Cancel");
        if (!confirmed) return;

        await _menuitems.DeleteAsync(Id);
        await _navigation.GoBackAsync();
    }
}
