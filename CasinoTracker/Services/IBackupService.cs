using CasinoTracker.Models;

namespace CasinoTracker.Services;

/// <summary>Exports the whole database to a file and restores it from one.</summary>
public interface IBackupService
{
    /// <summary>A file name for a new backup, e.g. <c>casinotracker-backup-20260919-1732.db3</c>.</summary>
    string SuggestFileName();

    /// <summary>
    /// Writes a snapshot of the database to a new file in the temp folder and returns its path.
    /// The caller owns the file and has to delete it after use.
    /// </summary>
    Task<string> CreateExportFileAsync();

    /// <summary>
    /// Checks that <paramref name="path"/> is an intact Casino Tracker database without touching the live data.
    /// </summary>
    /// <exception cref="InvalidBackupException">The file is not a usable backup.</exception>
    Task<BackupSummary> InspectAsync(string path);

    /// <summary>
    /// Validates the file and, if it is fine, replaces ALL current data with its content.
    /// The current data stays untouched when the file is rejected or cannot be applied.
    /// </summary>
    /// <exception cref="InvalidBackupException">The file is not a usable backup.</exception>
    Task<BackupSummary> ImportAsync(string path);
}

/// <summary>The selected file is not a usable Casino Tracker backup. The message is meant for the user.</summary>
public sealed class InvalidBackupException : Exception
{
    public InvalidBackupException(string message) : base(message)
    {
    }

    public InvalidBackupException(string message, Exception innerException) : base(message, innerException)
    {
    }
}
