using SQLite;

namespace CasinoTracker.Models;

[Table("PlayedGames")]
public class PlayedGame
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    [Indexed]
    public int SessionId { get; set; }

    [Indexed]
    public int GameId { get; set; }

    /// <summary>Won (positive) or lost (negative) amount.</summary>
    public decimal Amount { get; set; }

    /// <summary>Unix timestamp (seconds).</summary>
    public long Starttime { get; set; }

    /// <summary>Unix timestamp (seconds). Null while the game is being played.</summary>
    public long? Endtime { get; set; }
}
