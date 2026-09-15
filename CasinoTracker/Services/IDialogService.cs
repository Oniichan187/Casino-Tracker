namespace CasinoTracker.Services;

public interface IDialogService
{
    Task AlertAsync(string title, string message, string cancel = "OK");

    Task<bool> ConfirmAsync(string title, string message, string accept = "Yes", string cancel = "Cancel");

    Task<string?> PromptAsync(string title, string message, string? initialValue = null,
        string? placeholder = null, Keyboard? keyboard = null, string accept = "OK", string cancel = "Cancel");

    /// <summary>Shows an action sheet and returns the chosen button text (or null / the cancel text).</summary>
    Task<string?> ActionSheetAsync(string title, string cancel, string? destruction, params string[] buttons);
}
