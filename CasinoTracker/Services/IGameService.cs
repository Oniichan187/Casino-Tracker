using CasinoTracker.Models;

namespace CasinoTracker.Services;

public interface IGameService
{
    Task<List<Game>> GetAllAsync();
    Task<Game?> GetAsync(int id);
    Task<int> SaveAsync(Game game);

    /// <summary>Deletes the game and its casino links. Returns false when it was played in a session.</summary>
    Task<bool> DeleteAsync(int id);

    Task<int> CountPlayedAsync(int gameId);

    Task<List<int>> GetCasinoIdsAsync(int gameId);
    Task SetCasinosAsync(int gameId, IEnumerable<int> casinoIds);
}
