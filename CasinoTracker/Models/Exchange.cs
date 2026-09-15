using SQLite;

namespace CasinoTracker.Models;

/// <summary>
/// Money exchanged into chips (positive amount) or chips exchanged back into money (negative amount).
/// </summary>
[Table("Exchanges")]
public class Exchange
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    public decimal Amount { get; set; }

    [Indexed]
    public int SessionId { get; set; }

    /// <summary>Unix timestamp (seconds).</summary>
    public long Timestamp { get; set; }
}
