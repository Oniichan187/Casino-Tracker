using SQLite;

namespace CasinoTracker.Services;

public interface IDatabaseService
{
    /// <summary>Returns the (lazily initialised) connection with all tables created.</summary>
    Task<SQLiteAsyncConnection> GetConnectionAsync();

    /// <summary>
    /// Writes a consistent, self-contained snapshot of the whole database to <paramref name="destinationPath"/>
    /// (an existing file there is overwritten). Safe to call while the app is using the database.
    /// </summary>
    Task ExportToAsync(string destinationPath);

    /// <summary>
    /// Replaces the whole database with the given SQLite file and re-opens it; tables and columns that the
    /// file lacks are added exactly like on every start. If the file cannot be used, the previous database is
    /// restored and an exception is thrown. The caller has to validate the file beforehand and must not run
    /// other database work while the replacement is in progress.
    /// </summary>
    Task ReplaceWithAsync(string sourcePath);
}
