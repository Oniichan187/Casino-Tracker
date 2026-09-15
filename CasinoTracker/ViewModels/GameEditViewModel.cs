using System.Collections.ObjectModel;
using CasinoTracker.Models;
using CasinoTracker.Services;
using CasinoTracker.ViewModels.Items;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace CasinoTracker.ViewModels;

public partial class GameEditViewModel : BaseViewModel, IQueryAttributable
{
    private readonly IGameService _games;
    private readonly ICasinoService _casinos;
    private readonly IDialogService _dialogs;
    private readonly INavigationService _navigation;

    public GameEditViewModel(
        IGameService games,
        ICasinoService casinos,
        IDialogService dialogs,
        INavigationService navigation)
    {
        _games = games;
        _casinos = casinos;
        _dialogs = dialogs;
        _navigation = navigation;
        Title = "New game";
    }

    [ObservableProperty] private int _id;
    [ObservableProperty] private bool _isNew = true;
    [ObservableProperty] private string _name = string.Empty;
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
            Title = IsNew ? "New game" : "Edit game";

            var casinos = await _casinos.GetAllAsync();
            var selected = new HashSet<int>();

            if (!IsNew)
            {
                var game = await _games.GetAsync(Id);
                if (game is null)
                {
                    await _dialogs.AlertAsync("Not found", "This game does not exist anymore.");
                    await _navigation.GoBackAsync();
                    return;
                }

                Name = game.Name;
                selected = (await _games.GetCasinoIdsAsync(Id)).ToHashSet();
            }

            CasinoOptions.Clear();
            foreach (var c in casinos)
                CasinoOptions.Add(new SelectableItem(c.Id, c.Name, showPrice: false, c.Address) { IsSelected = selected.Contains(c.Id) });
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
            await _dialogs.AlertAsync("Name missing", "Please enter a name for the game.");
            return;
        }

        try
        {
            var game = new Game { Id = Id, Name = Name.Trim() };
            var id = await _games.SaveAsync(game);
            await _games.SetCasinosAsync(id, CasinoOptions.Where(o => o.IsSelected).Select(o => o.Id));
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

        var played = await _games.CountPlayedAsync(Id);
        if (played > 0)
        {
            await _dialogs.AlertAsync("Cannot delete", $"This game was played {played} time(s) in sessions. Delete those sessions first.");
            return;
        }

        var confirmed = await _dialogs.ConfirmAsync("Delete game?", $"Delete \"{Name}\"?", "Delete", "Cancel");
        if (!confirmed) return;

        await _games.DeleteAsync(Id);
        await _navigation.GoBackAsync();
    }
}
