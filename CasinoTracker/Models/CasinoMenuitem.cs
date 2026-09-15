using SQLite;

namespace CasinoTracker.Models;

/// <summary>Links a menu item to a casino together with its price there.</summary>
[Table("CasinoMenuitems")]
public class CasinoMenuitem
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    [Indexed]
    public int CasinoId { get; set; }

    [Indexed]
    public int MenuitemId { get; set; }

    public decimal Price { get; set; }
}
