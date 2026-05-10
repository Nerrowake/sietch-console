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
    private async Task ApplySchemaUpdatesAsync()
    {
        await AddColumnIfMissingAsync("BattlegroupProfiles", "CpuCount",          "INTEGER NOT NULL DEFAULT 4");
        await AddColumnIfMissingAsync("BattlegroupProfiles", "MemoryMb",          "INTEGER NOT NULL DEFAULT 8192");
        await AddColumnIfMissingAsync("BattlegroupProfiles", "VirtualSwitchName", "TEXT NULL");
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
}
