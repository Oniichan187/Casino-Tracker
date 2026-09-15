using SQLite;

namespace CasinoTracker.Models;

[Table("Casinos")]
public class Casino
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    [NotNull]
    public string Name { get; set; } = string.Empty;

    /// <summary>Optional street address.</summary>
    public string? Address { get; set; }

    /// <summary>Optional coordinates (stored as two nullable doubles).</summary>
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
}
