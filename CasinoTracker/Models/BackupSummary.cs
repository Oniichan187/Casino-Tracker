namespace CasinoTracker.Models;

/// <summary>What a backup file contains (read model), shown before an import is confirmed.</summary>
public sealed record BackupSummary(int Casinos, int Sessions);
