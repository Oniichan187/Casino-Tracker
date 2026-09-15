using CasinoTracker.Models;

namespace CasinoTracker.Services;

public interface IMenuitemService
{
    Task<List<Menuitem>> GetAllAsync();
    Task<Menuitem?> GetAsync(int id);
    Task<int> SaveAsync(Menuitem item);

    /// <summary>Deletes the item and its casino links. Returns false when it was consumed in a session.</summary>
    Task<bool> DeleteAsync(int id);

    Task<int> CountConsumedAsync(int menuitemId);

    /// <summary>All casinos where this item is available with its price.</summary>
    Task<List<CasinoMenuitem>> GetCasinoLinksAsync(int menuitemId);

    Task SetCasinosAsync(int menuitemId, IEnumerable<(int CasinoId, decimal Price)> entries);
}
