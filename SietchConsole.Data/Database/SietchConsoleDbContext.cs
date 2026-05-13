using Microsoft.EntityFrameworkCore;
using SietchConsole.Core.Models;

namespace SietchConsole.Data.Database;

public class SietchConsoleDbContext : DbContext
{
    public DbSet<ApplicationSettings>  ApplicationSettings  { get; set; }
    public DbSet<BattlegroupProfile>   BattlegroupProfiles  { get; set; }
    public DbSet<DiagnosticsResult>    DiagnosticsResults   { get; set; }
    public DbSet<BackupRecord>         BackupRecords         { get; set; }
    public DbSet<SetupWizardState>     SetupWizardStates    { get; set; }

    // ── M22: Multi-host support (#153) ───────────────────────────────────────
    public DbSet<HyperVHost>           HyperVHosts          { get; set; }

    // ── M23: Player management (#158, #160) ──────────────────────────────────
    public DbSet<BanRecord>            BanRecords           { get; set; }
    public DbSet<AllowlistEntry>       AllowlistEntries     { get; set; }

    // ── M24: Server metrics (#163, #164) ─────────────────────────────────────
    public DbSet<ServerMetricSnapshot> MetricSnapshots      { get; set; }
    public DbSet<DowntimeEvent>        DowntimeEvents       { get; set; }

    public SietchConsoleDbContext(DbContextOptions<SietchConsoleDbContext> options)
        : base(options) { }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ApplicationSettings>().HasKey(e => e.Id);
        modelBuilder.Entity<BattlegroupProfile>().HasKey(e => e.Id);
        modelBuilder.Entity<DiagnosticsResult>().HasKey(e => e.Id);
        modelBuilder.Entity<BackupRecord>().HasKey(e => e.Id);
        modelBuilder.Entity<SetupWizardState>().HasKey(e => e.Id);
        modelBuilder.Entity<HyperVHost>().HasKey(e => e.Id);
        modelBuilder.Entity<BanRecord>().HasKey(e => e.Id);
        modelBuilder.Entity<AllowlistEntry>().HasKey(e => e.Id);
        modelBuilder.Entity<ServerMetricSnapshot>().HasKey(e => e.Id);
        modelBuilder.Entity<DowntimeEvent>().HasKey(e => e.Id);

        base.OnModelCreating(modelBuilder);
    }
}
