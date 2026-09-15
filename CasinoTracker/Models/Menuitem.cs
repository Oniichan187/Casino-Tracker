using SQLite;

namespace CasinoTracker.Models;

[Table("Menuitems")]
public class Menuitem
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    [NotNull]
    public string Name { get; set; } = string.Empty;

    /// <summary>True = beverage, false = food.</summary>
    public bool Beverage { get; set; }
}
