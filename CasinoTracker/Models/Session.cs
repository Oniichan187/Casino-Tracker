using SQLite;

namespace CasinoTracker.Models;

[Table("Session")]
public class Session
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    /// <summary>
    /// User-editable display name. Not part of the original schema, but required
    /// because a running session can be renamed.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Unix timestamp (seconds).</summary>
    public long Starttime { get; set; }

    /// <summary>Unix timestamp (seconds). Null while the session is running.</summary>
    public long? Endtime { get; set; }

    [Indexed]
    public int CasinoId { get; set; }
}
