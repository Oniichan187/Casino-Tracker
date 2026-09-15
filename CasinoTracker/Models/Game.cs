using SQLite;

namespace CasinoTracker.Models;

[Table("Games")]
public class Game
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    [NotNull]
    public string Name { get; set; } = string.Empty;
}
