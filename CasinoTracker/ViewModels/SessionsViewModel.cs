using System.Collections.ObjectModel;
using CasinoTracker.Services;
using CasinoTracker.ViewModels.Items;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace CasinoTracker.ViewModels;

public partial class SessionsViewModel : BaseViewModel
{
    private readonly ISessionService _sessions;
    private readonly IDialogService _dialogs;
    private readonly INavigationService _navigation;

    public SessionsViewModel(ISessionService sessions, IDialogService dialogs, INavigationService navigation)
    {
        _sessions = sessions;
        _dialogs = dialogs;
        _navigation = navigation;
        Title = "Sessions";
    }

    [ObservableProperty] private bool _isEmpty;
    [ObservableProperty] private string _summaryText = string.Empty;

    public ObservableCollection<SessionListItem> Sessions { get; } = new();

    [RelayCommand]
    private async Task LoadAsync()
    {
        if (IsBusy) return;
        IsBusy = true;
        try
        {
            var summaries = await _sessions.GetSummariesAsync();
            Sessions.Clear();
            foreach (var s in summaries) Sessions.Add(new SessionListItem(s));

            IsEmpty = Sessions.Count == 0;
            SummaryText = Sessions.Count == 0
                ? string.Empty
                : $"{Sessions.Count} session{(Sessions.Count == 1 ? string.Empty : "s")}";
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
    private Task OpenAsync(SessionListItem? item)
    {
        if (item is null) return Task.CompletedTask;
        return _navigation.GoToAsync("sessiondetail", new Dictionary<string, object> { ["id"] = item.Id });
    }

    [RelayCommand]
    private async Task DeleteAsync(SessionListItem? item)
    {
        if (item is null) return;

        var confirmed = await _dialogs.ConfirmAsync(
            "Delete session?",
            $"\"{item.Name}\" including all exchanges, games and consumed items will be deleted permanently.",
            "Delete", "Cancel");
        if (!confirmed) return;

        await _sessions.DeleteSessionAsync(item.Id);
        Sessions.Remove(item);
        IsEmpty = Sessions.Count == 0;
    }
}
