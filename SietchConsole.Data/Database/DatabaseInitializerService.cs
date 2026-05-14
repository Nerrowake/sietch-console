using Microsoft.EntityFrameworkCore;

namespace SietchConsole.Data.Database;

public class DatabaseInitializerService
{
    private readonly SietchConsoleDbContext _db;

    public DatabaseInitializerService(SietchConsoleDbContext db) => _db = db;

    public async Task InitializeAsync()
    {
        await _db.Database.EnsureCreatedAsync();
        await ApplySchemaUpdatesAsync();
    }

    // EnsureCreated only creates the schema on first run and never alters existing tables.
    // New columns added to models after initial creation must be handled here via raw
    // ALTER TABLE so existing user databases get them automatically on next launch.
    // New tables must be created here with CREATE TABLE IF NOT EXISTS for existing DBs.
    private async Task ApplySchemaUpdatesAsync()
    {
        // ── Pre-M22 column migrations ────────────────────────────────────────────
        await AddColumnIfMissingAsync("BattlegroupProfiles",  "CpuCount",            "INTEGER NOT NULL DEFAULT 4");
        await AddColumnIfMissingAsync("BattlegroupProfiles",  "MemoryMb",            "INTEGER NOT NULL DEFAULT 8192");
        await AddColumnIfMissingAsync("BattlegroupProfiles",  "VirtualSwitchName",   "TEXT NULL");
        await AddColumnIfMissingAsync("ApplicationSettings",  "InstalledBuildId",    "TEXT NULL");
        await AddColumnIfMissingAsync("ApplicationSettings",  "AutoBackupEnabled",       "INTEGER NOT NULL DEFAULT 0");
        await AddColumnIfMissingAsync("ApplicationSettings",  "BackupIntervalHours",     "INTEGER NOT NULL DEFAULT 6");
        await AddColumnIfMissingAsync("ApplicationSettings",  "BackupRetainCount",       "INTEGER NOT NULL DEFAULT 10");
        await AddColumnIfMissingAsync("ApplicationSettings",  "RemoteManagementEnabled", "INTEGER NOT NULL DEFAULT 0");
        await AddColumnIfMissingAsync("ApplicationSettings",  "RemoteManagementPort",    "INTEGER NOT NULL DEFAULT 5151");
        await AddColumnIfMissingAsync("ApplicationSettings",  "RemoteManagementToken",   "TEXT NULL");

        // ── M22: Multi-host support (#153) ───────────────────────────────────────
        await AddColumnIfMissingAsync("BattlegroupProfiles", "HostId", "TEXT NULL");

        await CreateTableIfMissingAsync("HyperVHosts", """
            CREATE TABLE IF NOT EXISTS HyperVHosts (
                Id                TEXT    NOT NULL PRIMARY KEY,
                Name              TEXT    NOT NULL DEFAULT '',
                Hostname          TEXT    NOT NULL DEFAULT 'localhost',
                Port              INTEGER NOT NULL DEFAULT 5985,
                Username          TEXT    NULL,
                EncryptedPassword BLOB    NULL,
                IsLocal           INTEGER NOT NULL DEFAULT 0,
                CreatedAt         TEXT    NOT NULL DEFAULT (datetime('now'))
            )
            """);

        // ── M23: Player management (#158, #160) ──────────────────────────────────
        await CreateTableIfMissingAsync("BanRecords", """
            CREATE TABLE IF NOT EXISTS BanRecords (
                Id         INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
                PlayerId   TEXT    NOT NULL DEFAULT '',
                PlayerName TEXT    NOT NULL DEFAULT '',
                Reason     TEXT    NULL,
                BannedAt   TEXT    NOT NULL DEFAULT (datetime('now')),
                ExpiresAt  TEXT    NULL
            )
            """);

        await CreateTableIfMissingAsync("AllowlistEntries", """
            CREATE TABLE IF NOT EXISTS AllowlistEntries (
                Id          INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
                SteamId     TEXT    NOT NULL DEFAULT '',
                DisplayName TEXT    NULL,
                AddedAt     TEXT    NOT NULL DEFAULT (datetime('now'))
            )
            """);

        // ── M26: Discord webhook settings (#172) ─────────────────────────────────
        await AddColumnIfMissingAsync("ApplicationSettings", "DiscordWebhookEnabled",    "INTEGER NOT NULL DEFAULT 0");
        await AddColumnIfMissingAsync("ApplicationSettings", "DiscordWebhookUrl",         "TEXT NULL");
        await AddColumnIfMissingAsync("ApplicationSettings", "DiscordNotifyServerStart",  "INTEGER NOT NULL DEFAULT 1");
        await AddColumnIfMissingAsync("ApplicationSettings", "DiscordNotifyServerStop",   "INTEGER NOT NULL DEFAULT 1");
        await AddColumnIfMissingAsync("ApplicationSettings", "DiscordNotifyServerCrash",  "INTEGER NOT NULL DEFAULT 1");

        // ── M24: Server metrics (#163, #164) ─────────────────────────────────────
        await CreateTableIfMissingAsync("MetricSnapshots", """
            CREATE TABLE IF NOT EXISTS MetricSnapshots (
                Id            INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
                ProfileId     TEXT    NOT NULL DEFAULT '',
                Timestamp     TEXT    NOT NULL DEFAULT (datetime('now')),
                PlayerCount   INTEGER NOT NULL DEFAULT 0,
                CpuPercent    REAL    NOT NULL DEFAULT 0,
                MemoryMb      INTEGER NOT NULL DEFAULT 0,
                UptimeSeconds INTEGER NOT NULL DEFAULT 0
            )
            """);

        await CreateTableIfMissingAsync("DowntimeEvents", """
            CREATE TABLE IF NOT EXISTS DowntimeEvents (
                Id        INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
                ProfileId TEXT    NOT NULL DEFAULT '',
                StartedAt TEXT    NOT NULL DEFAULT (datetime('now')),
                EndedAt   TEXT    NULL,
                Reason    TEXT    NOT NULL DEFAULT 'Unknown'
            )
            """);
    }

    private async Task AddColumnIfMissingAsync(string table, string column, string definition)
    {
        try
        {
            // SQLite throws "duplicate column name" if the column already exists;
            // that is the normal "already migrated" path — swallow and move on.
            // EF1002: all three arguments are hardcoded literals — no injection risk.
#pragma warning disable EF1002
            await _db.Database.ExecuteSqlRawAsync(
                $"ALTER TABLE {table} ADD COLUMN {column} {definition}");
#pragma warning restore EF1002
        }
        catch
        {
            // Column already exists — nothing to do.
        }
    }

    private async Task CreateTableIfMissingAsync(string tableName, string createSql)
    {
        // We rely on "CREATE TABLE IF NOT EXISTS" idempotency — no try/catch needed.
#pragma warning disable EF1002
        await _db.Database.ExecuteSqlRawAsync(createSql);
#pragma warning restore EF1002
    }
}
