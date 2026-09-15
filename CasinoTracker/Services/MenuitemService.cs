using CasinoTracker.Models;

namespace CasinoTracker.Services;

public sealed class MenuitemService : IMenuitemService
{
    private readonly IDatabaseService _db;

    public MenuitemService(IDatabaseService db)
    {
        _db = db;
    }

    public async Task<List<Menuitem>> GetAllAsync()
    {
        var conn = await _db.GetConnectionAsync();
        var items = await conn.Table<Menuitem>().ToListAsync();
        return items
            .OrderBy(m => m.Beverage)
            .ThenBy(m => m.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }

    public async Task<Menuitem?> GetAsync(int id)
    {
        var conn = await _db.GetConnectionAsync();
        return await conn.FindAsync<Menuitem>(id);
    }

    public async Task<int> SaveAsync(Menuitem item)
    {
        var conn = await _db.GetConnectionAsync();
        if (item.Id == 0)
            await conn.InsertAsync(item);
        else
            await conn.UpdateAsync(item);
        return item.Id;
    }

    public async Task<int> CountConsumedAsync(int menuitemId)
    {
        var conn = await _db.GetConnectionAsync();
        return await conn.Table<ConsumedMenuitem>().Where(c => c.MenuitemId == menuitemId).CountAsync();
    }

    public async Task<bool> DeleteAsync(int id)
    {
        if (await CountConsumedAsync(id) > 0) return false;

        var conn = await _db.GetConnectionAsync();
        await conn.RunInTransactionAsync(t =>
        {
            t.Table<CasinoMenuitem>().Delete(x => x.MenuitemId == id);
            t.Delete<Menuitem>(id);
        });
        return true;
    }

    public async Task<List<CasinoMenuitem>> GetCasinoLinksAsync(int menuitemId)
    {
        var conn = await _db.GetConnectionAsync();
        return await conn.Table<CasinoMenuitem>().Where(x => x.MenuitemId == menuitemId).ToListAsync();
    }

    public async Task SetCasinosAsync(int menuitemId, IEnumerable<(int CasinoId, decimal Price)> entries)
    {
        var conn = await _db.GetConnectionAsync();
        var list = entries.GroupBy(e => e.CasinoId).Select(g => g.First()).ToList();
        await conn.RunInTransactionAsync(t =>
        {
            t.Table<CasinoMenuitem>().Delete(x => x.MenuitemId == menuitemId);
            foreach (var (casinoId, price) in list)
                t.Insert(new CasinoMenuitem { CasinoId = casinoId, MenuitemId = menuitemId, Price = price });
        });
    }
}
