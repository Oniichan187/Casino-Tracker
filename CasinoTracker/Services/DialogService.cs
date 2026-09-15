namespace CasinoTracker.Services;

public sealed class DialogService : IDialogService
{
    private static Page? CurrentPage =>
        Shell.Current?.CurrentPage ?? Application.Current?.Windows.FirstOrDefault()?.Page;

    public Task AlertAsync(string title, string message, string cancel = "OK") =>
        CurrentPage?.DisplayAlertAsync(title, message, cancel) ?? Task.CompletedTask;

    public Task<bool> ConfirmAsync(string title, string message, string accept = "Yes", string cancel = "Cancel") =>
        CurrentPage?.DisplayAlertAsync(title, message, accept, cancel) ?? Task.FromResult(false);

    public Task<string?> PromptAsync(string title, string message, string? initialValue = null,
        string? placeholder = null, Keyboard? keyboard = null, string accept = "OK", string cancel = "Cancel") =>
        CurrentPage?.DisplayPromptAsync(title, message, accept, cancel, placeholder, -1, keyboard, initialValue ?? string.Empty)
        ?? Task.FromResult<string?>(null);

    public Task<string?> ActionSheetAsync(string title, string cancel, string? destruction, params string[] buttons) =>
        CurrentPage?.DisplayActionSheetAsync(title, cancel, destruction, buttons)
        ?? Task.FromResult<string?>(null);
}
