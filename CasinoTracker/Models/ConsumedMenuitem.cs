using SQLite;

namespace CasinoTracker.Models;

[Table("ConsumedMenuitems")]
public class ConsumedMenuitem
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    [Indexed]
    public int SessionId { get; set; }

    [Indexed]
    public int MenuitemId { get; set; }

    /// <summary>Unix timestamp (seconds).</summary>
    public long Timestamp { get; set; }

    /// <summary>
    /// Price at the time of consumption (snapshot of the casino's menu price).
    /// Additive column on top of the requested schema so that later price changes or
    /// menu edits do not rewrite history. Null for rows written before this column existed;
    /// readers then fall back to the current casino price.
    /// </summary>
    public decimal? Price { get; set; }
}
