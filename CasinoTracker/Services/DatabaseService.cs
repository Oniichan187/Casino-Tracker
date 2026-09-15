using CasinoTracker.Models;
using SQLite;

namespace CasinoTracker.Services;

public sealed class DatabaseService : IDatabaseService
{
    public const string FileName = "casinotracker.db3";

    private const SQLiteOpenFlags Flags =
        SQLiteOpenFlags.ReadWrite | SQLiteOpenFlags.Create | SQLiteOpenFlags.SharedCache;

    private readonly SemaphoreSlim _gate = new(1, 1);
    private SQLiteAsyncConnection? _connection;

    public async Task<SQLiteAsyncConnection> GetConnectionAsync()
    {
        if (_connection is { } ready) return ready;

        await _gate.WaitAsync();
        try
        {
            if (_connection is not null) return _connection;

            var path = Path.Combine(FileSystem.AppDataDirectory, FileName);
            var connection = new SQLiteAsyncConnection(path, Flags);
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

            _connection = connection;
            return connection;
        }
        finally
        {
            _gate.Release();
        }
    }
}
