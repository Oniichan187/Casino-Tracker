namespace CasinoTracker.Services;

/// <summary>Moves files between the app's private storage and a location the user chooses in the file system.</summary>
public interface IFileTransferService
{
    /// <summary>
    /// Lets the user choose where to save and copies <paramref name="sourcePath"/> there under the suggested name.
    /// Returns false if the user cancelled.
    /// </summary>
    Task<bool> SaveAsync(string sourcePath, string suggestedFileName);

    /// <summary>
    /// Lets the user pick a file and returns the path of a private temp copy (the caller deletes it),
    /// or null if the user cancelled.
    /// </summary>
    Task<string?> PickAsync();
}
