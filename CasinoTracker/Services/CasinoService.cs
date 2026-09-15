using CasinoTracker.Models;

namespace CasinoTracker.Services;

public sealed class CasinoService : ICasinoService
{
    private readonly IDatabaseService _db;

    public CasinoService(IDatabaseService db)
    {
        _db = db;
    }

    public async Task<List<Casino>> GetAllAsync()
    {
        var conn = await _db.GetConnectionAsync();
        var casinos = await conn.Table<Casino>().ToListAsync();
        return casinos.OrderBy(c => c.Name, StringComparer.CurrentCultureIgnoreCase).ToList();
    }

    public async Task<Casino?> GetAsync(int id)
    {
        var conn = await _db.GetConnectionAsync();
        return await conn.FindAsync<Casino>(id);
    }

    public async Task<int> SaveAsync(Casino casino)
    {
        var conn = await _db.GetConnectionAsync();
        if (casino.Id == 0)
            await conn.InsertAsync(casino);
        else
            await conn.UpdateAsync(casino);
        return casino.Id;
    }

    public async Task<int> CountSessionsAsync(int casinoId)
    {
        var conn = await _db.GetConnectionAsync();
        return await conn.Table<Session>().Where(s => s.CasinoId == casinoId).CountAsync();
    }

    public async Task<bool> DeleteAsync(int id)
    {
        if (await CountSessionsAsync(id) > 0) return false;

        var conn = await _db.GetConnectionAsync();
        await conn.RunInTransactionAsync(t =>
        {
            t.Table<CasinoGame>().Delete(x => x.CasinoId == id);
            t.Table<CasinoMenuitem>().Delete(x => x.CasinoId == id);
            t.Delete<Casino>(id);
        });
        return true;
    }

    public async Task<List<Game>> GetGamesAsync(int casinoId)
    {
        var conn = await _db.GetConnectionAsync();
        var links = await GetGameLinksAsync(casinoId);
        if (links.Count == 0) return new List<Game>();

        var ids = links.Select(l => l.GameId).ToHashSet();
        var games = await conn.Table<Game>().ToListAsync();
        return games.Where(g => ids.Contains(g.Id)).OrderBy(g => g.Name, StringComparer.CurrentCultureIgnoreCase).ToList();
    }

    public async Task<List<CasinoGame>> GetGameLinksAsync(int casinoId)
    {
        var conn = await _db.GetConnectionAsync();
        return await conn.Table<CasinoGame>().Where(x => x.CasinoId == casinoId).ToListAsync();
    }

    public async Task SetGamesAsync(int casinoId, IEnumerable<int> gameIds)
    {
        var conn = await _db.GetConnectionAsync();
        var distinct = gameIds.Distinct().ToList();
        await conn.RunInTransactionAsync(t =>
        {
            t.Table<CasinoGame>().Delete(x => x.CasinoId == casinoId);
            foreach (var gameId in distinct)
                t.Insert(new CasinoGame { CasinoId = casinoId, GameId = gameId });
        });
    }

    public async Task<List<MenuEntry>> GetMenuAsync(int casinoId)
    {
        var conn = await _db.GetConnectionAsync();
        var links = await GetMenuLinksAsync(casinoId);
        if (links.Count == 0) return new List<MenuEntry>();

        var items = (await conn.Table<Menuitem>().ToListAsync()).ToDictionary(m => m.Id);
        return links
            .Where(l => items.ContainsKey(l.MenuitemId))
            .Select(l => new MenuEntry(items[l.MenuitemId], l.Price))
            .OrderBy(e => e.Item.Beverage)
            .ThenBy(e => e.Item.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }

    public async Task<List<CasinoMenuitem>> GetMenuLinksAsync(int casinoId)
    {
        var conn = await _db.GetConnectionAsync();
        return await conn.Table<CasinoMenuitem>().Where(x => x.CasinoId == casinoId).ToListAsync();
    }

    public async Task SetMenuAsync(int casinoId, IEnumerable<(int MenuitemId, decimal Price)> entries)
    {
        var conn = await _db.GetConnectionAsync();
        var list = entries.GroupBy(e => e.MenuitemId).Select(g => g.First()).ToList();
        await conn.RunInTransactionAsync(t =>
        {
            t.Table<CasinoMenuitem>().Delete(x => x.CasinoId == casinoId);
            foreach (var (menuitemId, price) in list)
                t.Insert(new CasinoMenuitem { CasinoId = casinoId, MenuitemId = menuitemId, Price = price });
        });
    }
}
