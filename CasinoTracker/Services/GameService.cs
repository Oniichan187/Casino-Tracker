using CasinoTracker.Models;

namespace CasinoTracker.Services;

public sealed class GameService : IGameService
{
    private readonly IDatabaseService _db;

    public GameService(IDatabaseService db)
    {
        _db = db;
    }

    public async Task<List<Game>> GetAllAsync()
    {
        var conn = await _db.GetConnectionAsync();
        var games = await conn.Table<Game>().ToListAsync();
        return games.OrderBy(g => g.Name, StringComparer.CurrentCultureIgnoreCase).ToList();
    }

    public async Task<Game?> GetAsync(int id)
    {
        var conn = await _db.GetConnectionAsync();
        return await conn.FindAsync<Game>(id);
    }

    public async Task<int> SaveAsync(Game game)
    {
        var conn = await _db.GetConnectionAsync();
        if (game.Id == 0)
            await conn.InsertAsync(game);
        else
            await conn.UpdateAsync(game);
        return game.Id;
    }

    public async Task<int> CountPlayedAsync(int gameId)
    {
        var conn = await _db.GetConnectionAsync();
        return await conn.Table<PlayedGame>().Where(p => p.GameId == gameId).CountAsync();
    }

    public async Task<bool> DeleteAsync(int id)
    {
        if (await CountPlayedAsync(id) > 0) return false;

        var conn = await _db.GetConnectionAsync();
        await conn.RunInTransactionAsync(t =>
        {
            t.Table<CasinoGame>().Delete(x => x.GameId == id);
            t.Delete<Game>(id);
        });
        return true;
    }

    public async Task<List<int>> GetCasinoIdsAsync(int gameId)
    {
        var conn = await _db.GetConnectionAsync();
        var links = await conn.Table<CasinoGame>().Where(x => x.GameId == gameId).ToListAsync();
        return links.Select(l => l.CasinoId).Distinct().ToList();
    }

    public async Task SetCasinosAsync(int gameId, IEnumerable<int> casinoIds)
    {
        var conn = await _db.GetConnectionAsync();
        var list = casinoIds.Distinct().ToList();
        await conn.RunInTransactionAsync(t =>
        {
            t.Table<CasinoGame>().Delete(x => x.GameId == gameId);
            foreach (var casinoId in list)
                t.Insert(new CasinoGame { CasinoId = casinoId, GameId = gameId });
        });
    }
}
