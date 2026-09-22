using CasinoTracker.Models;
using SQLite;

namespace CasinoTracker.Services;

public sealed class BackupService : IBackupService
{
    /// <summary>
    /// Tables that every backup written by this app contains, because the schema is created in one go on the
    /// first start. Tables added by later versions are deliberately NOT listed: an older backup lacks them and
    /// must stay importable - the normal schema migration creates them right after the import.
    /// </summary>
    internal static readonly IReadOnlyList<string> RequiredTables = new[]
    {
        "Casinos", "Menuitems", "Games", "CasinoMenuitems", "CasinoGame",
        "Session", "Exchanges", "ConsumedMenuitems", "PlayedGames",
    };

    private static readonly byte[] SqliteHeader = "SQLite format 3\0"u8.ToArray();

    private readonly IDatabaseService _database;

    public BackupService(IDatabaseService database)
    {
        _database = database;
    }

    public string SuggestFileName() => FileNameFor(DateTime.Now);

    internal static string FileNameFor(DateTime timestamp) =>
        $"casinotracker-backup-{timestamp:yyyyMMdd-HHmm}.db3";

    public async Task<string> CreateExportFileAsync()
    {
        var path = Path.Combine(Path.GetTempPath(), $"casinotracker-export-{Guid.NewGuid():N}.db3");
        try
        {
            await _database.ExportToAsync(path);
            return path;
        }
        catch
        {
            TryDelete(path);
            throw;
        }
    }

    public Task<BackupSummary> InspectAsync(string path) => Task.Run(() => Inspect(path));

    public async Task<BackupSummary> ImportAsync(string path)
    {
        // Never hand an unchecked file to the live database.
        var summary = await InspectAsync(path);
        await _database.ReplaceWithAsync(path);
        return summary;
    }

    private static BackupSummary Inspect(string path)
    {
        if (!File.Exists(path))
            throw new InvalidBackupException("The selected file could not be found.");
        if (new FileInfo(path).Length == 0)
            throw new InvalidBackupException("The selected file is empty.");
        if (!HasSqliteHeader(path))
            throw new InvalidBackupException("The selected file is not a Casino Tracker backup (it is not a database file).");

        try
        {
            using var db = new SQLiteConnection(path, SQLiteOpenFlags.ReadOnly);

            var integrity = db.ExecuteScalar<string>("PRAGMA integrity_check");
            if (!string.Equals(integrity, "ok", StringComparison.OrdinalIgnoreCase))
                throw new InvalidBackupException("The backup file is damaged (the database integrity check failed).");

            var tables = db.QueryScalars<string>("SELECT name FROM sqlite_master WHERE type = 'table'")
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            var missing = RequiredTables.Where(t => !tables.Contains(t)).ToList();
            if (missing.Count > 0)
                throw new InvalidBackupException(
                    $"The selected file is not a Casino Tracker backup (missing: {string.Join(", ", missing)}).");

            return new BackupSummary(
                Casinos: db.ExecuteScalar<int>("SELECT COUNT(*) FROM Casinos"),
                Sessions: db.ExecuteScalar<int>("SELECT COUNT(*) FROM Session"));
        }
        catch (SQLiteException ex)
        {
            // Truncated or otherwise malformed files surface here instead of as a failed integrity check.
            throw new InvalidBackupException("The backup file is damaged or cannot be read.", ex);
        }
    }

    private static bool HasSqliteHeader(string path)
    {
        Span<byte> header = stackalloc byte[16];
        using var stream = File.OpenRead(path);
        return stream.ReadAtLeast(header, header.Length, throwOnEndOfStream: false) == header.Length
               && header.SequenceEqual(SqliteHeader);
    }

    private static void TryDelete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch
        {
            // best effort
        }
    }
}
