using Microsoft.EntityFrameworkCore;
using SietchConsole.Core.Models;

namespace SietchConsole.Data.Database;

public class SietchConsoleDbContext : DbContext
{
    public DbSet<ApplicationSettings> ApplicationSettings { get; set; }
    public DbSet<BattlegroupProfile>  BattlegroupProfiles  { get; set; }
    public DbSet<DiagnosticsResult>   DiagnosticsResults   { get; set; }
    public DbSet<BackupRecord>        BackupRecords         { get; set; }

    public SietchConsoleDbContext(DbContextOptions<SietchConsoleDbContext> options)
        : base(options) { }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ApplicationSettings>().HasKey(e => e.Id);
        modelBuilder.Entity<BattlegroupProfile>().HasKey(e => e.Id);
        modelBuilder.Entity<DiagnosticsResult>().HasKey(e => e.Id);
        modelBuilder.Entity<BackupRecord>().HasKey(e => e.Id);

        base.OnModelCreating(modelBuilder);
    }
}
