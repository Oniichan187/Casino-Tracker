namespace CasinoTracker.Services;

public sealed class NavigationService : INavigationService
{
    public Task GoToAsync(string route, IDictionary<string, object>? parameters = null)
    {
        if (Shell.Current is null) return Task.CompletedTask;
        return parameters is null
            ? Shell.Current.GoToAsync(route)
            : Shell.Current.GoToAsync(route, parameters);
    }

    public Task GoBackAsync() => Shell.Current?.GoToAsync("..") ?? Task.CompletedTask;
}
