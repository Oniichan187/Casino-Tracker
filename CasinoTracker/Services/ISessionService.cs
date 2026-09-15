using CasinoTracker.Helpers;
using CasinoTracker.Models;

namespace CasinoTracker.Services;

public interface ISessionService
{
    // Session lifecycle
    Task<Session?> GetActiveSessionAsync();
    Task<Session?> GetAsync(int sessionId);
    Task<Session> StartSessionAsync(int casinoId, string? name);
    Task UpdateSessionNameAsync(int sessionId, string name);
    Task EndSessionAsync(int sessionId, long? endTime = null);
    Task DeleteSessionAsync(int sessionId);

    /// <summary>Closes all running sessions that exceed the given duration. Returns the number closed.</summary>
    Task<int> AutoCloseExpiredSessionsAsync(TimeSpan maxDuration);

    // Read models
    Task<List<SessionSummary>> GetSummariesAsync();
    Task<SessionDetail?> GetDetailAsync(int sessionId);
    Task<BankrollSnapshot> GetBankrollAsync(int sessionId);

    // Exchanges
    Task<Exchange> AddExchangeAsync(int sessionId, decimal amount);
    Task<List<Exchange>> GetExchangesAsync(int sessionId);
    Task UpdateExchangeAmountAsync(int exchangeId, decimal amount);
    Task DeleteExchangeAsync(int exchangeId);

    // Games
    Task<List<PlayedGame>> GetPlayedGamesAsync(int sessionId);
    Task<PlayedGame?> GetOpenPlayedGameAsync(int sessionId);
    Task<PlayedGame> StartGameAsync(int sessionId, int gameId);
    Task StopGameAsync(int playedGameId, decimal amount);
    Task UpdatePlayedGameAmountAsync(int playedGameId, decimal amount);
    Task DeletePlayedGameAsync(int playedGameId);

    // Consumption
    Task<List<ConsumedMenuitem>> GetConsumedAsync(int sessionId);
    Task<ConsumedMenuitem> AddConsumedAsync(int sessionId, int menuitemId);
    Task<bool> RemoveLastConsumedAsync(int sessionId, int menuitemId);
    Task DeleteConsumedAsync(int consumedId);
}
