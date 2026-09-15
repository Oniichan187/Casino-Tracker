using SQLite;

namespace CasinoTracker.Models;

/// <summary>Links a game to a casino where it is available.</summary>
[Table("CasinoGame")]
public class CasinoGame
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    [Indexed]
    public int CasinoId { get; set; }

    [Indexed]
    public int GameId { get; set; }
}
