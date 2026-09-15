using CasinoTracker.Models;
using CasinoTracker.Services;
using SQLite;

namespace CasinoTracker.Tests;

/// <summary>
/// IDatabaseService backed by a throw-away SQLite file so the real services can be exercised.
/// </summary>
public sealed class TestDatabase : IDatabaseService, IAsyncDisposable
{
    private readonly string _path = Path.Combine(Path.GetTempPath(), $"casinotracker-test-{Guid.NewGuid():N}.db3");
    private readonly Lazy<Task<SQLiteAsyncConnection>> _connection;

    public TestDatabase()
    {
        _connection = new Lazy<Task<SQLiteAsyncConnection>>(async () =>
        {
            var conn = new SQLiteAsyncConnection(_path, SQLiteOpenFlags.ReadWrite | SQLiteOpenFlags.Create);
            await conn.CreateTablesAsync<Casino, Menuitem, Game, CasinoMenuitem, CasinoGame>();
            await conn.CreateTablesAsync<Session, Exchange, ConsumedMenuitem, PlayedGame>();
            return conn;
        });
    }

    public Task<SQLiteAsyncConnection> GetConnectionAsync() => _connection.Value;

    public async ValueTask DisposeAsync()
    {
        if (_connection.IsValueCreated)
        {
            var conn = await _connection.Value;
            await conn.CloseAsync();
        }

        try
        {
            File.Delete(_path);
        }
        catch
        {
            // best effort
        }
    }
}
