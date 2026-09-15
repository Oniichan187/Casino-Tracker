using SQLite;

namespace CasinoTracker.Services;

public interface IDatabaseService
{
    /// <summary>Returns the (lazily initialised) connection with all tables created.</summary>
    Task<SQLiteAsyncConnection> GetConnectionAsync();
}
