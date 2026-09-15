using CasinoTracker.Models;

namespace CasinoTracker.Services;

public interface ICasinoService
{
    Task<List<Casino>> GetAllAsync();
    Task<Casino?> GetAsync(int id);

    /// <summary>Inserts or updates and returns the id.</summary>
    Task<int> SaveAsync(Casino casino);

    /// <summary>Deletes the casino and its links. Returns false when sessions reference it.</summary>
    Task<bool> DeleteAsync(int id);

    Task<int> CountSessionsAsync(int casinoId);

    Task<List<Game>> GetGamesAsync(int casinoId);
    Task<List<CasinoGame>> GetGameLinksAsync(int casinoId);
    Task SetGamesAsync(int casinoId, IEnumerable<int> gameIds);

    Task<List<MenuEntry>> GetMenuAsync(int casinoId);
    Task<List<CasinoMenuitem>> GetMenuLinksAsync(int casinoId);
    Task SetMenuAsync(int casinoId, IEnumerable<(int MenuitemId, decimal Price)> entries);
}
