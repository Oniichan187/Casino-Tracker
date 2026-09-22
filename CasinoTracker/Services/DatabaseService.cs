using CasinoTracker.Models;
using SQLite;

namespace CasinoTracker.Services;

public sealed class DatabaseService : IDatabaseService, IAsyncDisposable
{
    public const string FileName = "casinotracker.db3";

    private const SQLiteOpenFlags Flags =
        SQLiteOpenFlags.ReadWrite | SQLiteOpenFlags.Create | SQLiteOpenFlags.SharedCache;

    /// <summary>SQLite side files that belong to the main database file and must never outlive it.</summary>
    private static readonly string[] SideFileSuffixes = { "-journal", "-wal", "-shm" };

    private readonly string _path;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private SQLiteAsyncConnection? _connection;

    public DatabaseService(string databasePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(databasePath);
        _path = databasePath;
    }

    public async Task<SQLiteAsyncConnection> GetConnectionAsync()
    {
        if (_connection is { } ready) return ready;

        await _gate.WaitAsync();
        try
        {
            return _connection ??= await OpenAsync();
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task ExportToAsync(string destinationPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(destinationPath);

        // Holding the gate keeps an export from overlapping a replacement.
        await _gate.WaitAsync();
        try
        {
            var connection = _connection ??= await OpenAsync();

            // VACUUM INTO writes a transactionally consistent, defragmented copy (no journal / WAL side files),
            // but refuses to write into an existing non-empty file.
            if (File.Exists(destinationPath)) File.Delete(destinationPath);
            await connection.ExecuteAsync("VACUUM INTO ?", destinationPath);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task ReplaceWithAsync(string sourcePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourcePath);
        if (!File.Exists(sourcePath))
            throw new FileNotFoundException("The backup file was not found.", sourcePath);

        await _gate.WaitAsync();
        try
        {
            var rollbackPath = _path + ".rollback";
            var incomingPath = _path + ".incoming";

            // Release the file first; everything below works on closed files.
            await CloseAsync();

            var hadDatabase = File.Exists(_path);
            if (hadDatabase) File.Copy(_path, rollbackPath, overwrite: true);

            try
            {
                // Stage the new file next to the database so the final step is a rename, not a long copy.
                File.Copy(sourcePath, incomingPath, overwrite: true);

                // A leftover journal of the OLD database would be "rolled back" into the NEW file on the next
                // open and corrupt it, so the side files have to go before the file is swapped.
                DeleteSideFiles();
                File.Move(incomingPath, _path, overwrite: true);

                // Opening also runs the schema migration, which proves the file is usable by this app version.
                _connection = await OpenAsync();
            }
            catch (Exception failure)
            {
                try
                {
                    await CloseAsync();
                    DeleteSideFiles();
                    if (hadDatabase) File.Copy(rollbackPath, _path, overwrite: true);
                    else File.Delete(_path);
                }
                catch (Exception restoreFailure)
                {
                    // The rollback copy is deliberately left in place for manual recovery.
                    throw new IOException(
                        $"The backup could not be applied ({failure.Message}) and the previous data could not be restored ({restoreFailure.Message}).",
                        failure);
                }

                TryDelete(rollbackPath);
                throw new IOException($"The backup could not be used ({failure.Message}). Your existing data was kept.", failure);
            }
            finally
            {
                TryDelete(incomingPath);
            }

            TryDelete(rollbackPath);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        await _gate.WaitAsync();
        try
        {
            await CloseAsync();
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>Opens the database and brings its schema up to date. Must be called under the gate.</summary>
    private async Task<SQLiteAsyncConnection> OpenAsync()
    {
        var connection = new SQLiteAsyncConnection(_path, Flags);
        try
        {
            // CreateTables also adds columns that were introduced later (e.g. ConsumedMenuitems.Price).
            await connection.CreateTablesAsync<Casino, Menuitem, Game, CasinoMenuitem, CasinoGame>();
            await connection.CreateTablesAsync<Session, Exchange, ConsumedMenuitem, PlayedGame>();
        }
        catch
        {
            // Do not cache a failed initialisation - the next call retries.
            try
            {
                await connection.CloseAsync();
            }
            catch
            {
                // never let the cleanup hide the original failure
            }
            throw;
        }

        return connection;
    }

    /// <summary>Closes the cached connection, if any. Must be called under the gate.</summary>
    private async Task CloseAsync()
    {
        if (_connection is not { } open) return;

        _connection = null;
        await open.CloseAsync();
    }

    private void DeleteSideFiles()
    {
        foreach (var suffix in SideFileSuffixes)
            File.Delete(_path + suffix); // no-op when missing; a real failure must abort the swap
    }

    private static void TryDelete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch
        {
            // best effort
        }
    }
}
