using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SietchConsole.Core.Interfaces;
using SietchConsole.Core.Models;
using SietchConsole.Data.Database;

namespace Sietch_Console.Services.Players;

/// <summary>
/// Manages connected players, bans, and the allowlist (#158, #159, #160).
/// Player detection is log-based (OutputLineReceived); kick/ban commands are
/// sent to the server process via stdin using IServerProcessService.SendCommandAsync.
/// </summary>
public sealed class PlayerManagementService : IPlayerManagementService, IDisposable
{
    // ── Log-pattern regexes ───────────────────────────────────────────────────

    // TODO: Validate these patterns against real Dune: Awakening server logs before v1.0.
    // These are heuristic guesses based on UE5 dedicated server conventions.
    private static readonly Regex JoinPattern = new(
        @"(?i)player\s+(?:joined|connected)[:\s]+(?<name>[^\s(]+).*?(?:id|eosid)[:\s]+(?<id>[\w\-]+)",
        RegexOptions.Compiled);

    private static readonly Regex LeavePattern = new(
        @"(?i)player\s+(?:left|disconnected|quit)[:\s]+(?<name>[^\s(]+).*?(?:id|eosid)[:\s]+(?<id>[\w\-]+)",
        RegexOptions.Compiled);

    // ── State ─────────────────────────────────────────────────────────────────

    private readonly IServiceScopeFactory         _scopeFactory;
    private readonly IServerProcessService        _process;
    private readonly ILogger<PlayerManagementService> _log;

    private readonly ConcurrentDictionary<string, PlayerInfo> _connected = new();

    // ── IPlayerManagementService ──────────────────────────────────────────────

    public IReadOnlyList<PlayerInfo> ConnectedPlayers
        => _connected.Values.ToList().AsReadOnly();

    public event EventHandler? PlayersChanged;

    public PlayerManagementService(
        IServiceScopeFactory scopeFactory,
        IServerProcessService processService,
        ILogger<PlayerManagementService> log)
    {
        _scopeFactory = scopeFactory;
        _process      = processService;
        _log          = log;

        _process.OutputLineReceived += OnOutputLine;
        _process.ProcessExited      += OnProcessExited;
    }

    // ── Connected players ─────────────────────────────────────────────────────

    public Task<IReadOnlyList<PlayerInfo>> GetConnectedPlayersAsync()
        => Task.FromResult(ConnectedPlayers);

    // ── Kick / Ban ────────────────────────────────────────────────────────────

    public async Task KickPlayerAsync(string playerId, string? reason = null)
    {
        // The server console command format is TBD — using a common UE5 convention.
        // TODO: Verify the actual kick command format with Funcom's server docs.
        var cmd = string.IsNullOrWhiteSpace(reason)
            ? $"kick {playerId}"
            : $"kick {playerId} {reason}";

        await _process.SendCommandAsync(cmd);
        _log.LogInformation("Sent kick command for player {PlayerId}.", playerId);
    }

    public async Task BanPlayerAsync(
        string playerId, string playerName,
        string? reason = null, TimeSpan? duration = null)
    {
        // Persist the ban record.
        var record = new BanRecord
        {
            PlayerId   = playerId,
            PlayerName = playerName,
            Reason     = reason,
            BannedAt   = DateTime.UtcNow,
            ExpiresAt  = duration.HasValue ? DateTime.UtcNow + duration.Value : null,
        };

        using (var scope = _scopeFactory.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SietchConsoleDbContext>();
            db.BanRecords.Add(record);
            await db.SaveChangesAsync();
        }

        // Send the server ban command.
        // TODO: Verify the actual ban command format with Funcom's server docs.
        var cmd = string.IsNullOrWhiteSpace(reason)
            ? $"ban {playerId}"
            : $"ban {playerId} {reason}";

        await _process.SendCommandAsync(cmd);
        _log.LogInformation("Banned player {PlayerName} ({PlayerId}).", playerName, playerId);
    }

    public async Task UnbanPlayerAsync(int banRecordId)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SietchConsoleDbContext>();

        var record = await db.BanRecords.FindAsync(banRecordId);
        if (record is null) return;

        db.BanRecords.Remove(record);
        await db.SaveChangesAsync();

        _log.LogInformation("Removed ban record {Id} for {PlayerName}.",
            banRecordId, record.PlayerName);
    }

    public async Task<IReadOnlyList<BanRecord>> GetBanListAsync()
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SietchConsoleDbContext>();
        return await db.BanRecords
                       .OrderByDescending(r => r.BannedAt)
                       .ToListAsync();
    }

    // ── Allowlist ─────────────────────────────────────────────────────────────

    public async Task<IReadOnlyList<AllowlistEntry>> GetAllowlistAsync()
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SietchConsoleDbContext>();
        return await db.AllowlistEntries
                       .OrderBy(e => e.DisplayName ?? e.SteamId)
                       .ToListAsync();
    }

    public async Task AddToAllowlistAsync(string steamId, string? displayName = null)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SietchConsoleDbContext>();

        // Avoid duplicates.
        var exists = await db.AllowlistEntries.AnyAsync(e => e.SteamId == steamId);
        if (exists) return;

        db.AllowlistEntries.Add(new AllowlistEntry
        {
            SteamId     = steamId,
            DisplayName = displayName,
            AddedAt     = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();
    }

    public async Task RemoveFromAllowlistAsync(int entryId)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SietchConsoleDbContext>();

        var entry = await db.AllowlistEntries.FindAsync(entryId);
        if (entry is null) return;

        db.AllowlistEntries.Remove(entry);
        await db.SaveChangesAsync();
    }

    // ── Log-parsing ───────────────────────────────────────────────────────────

    private void OnOutputLine(object? sender, string line)
    {
        var joinMatch = JoinPattern.Match(line);
        if (joinMatch.Success)
        {
            var id   = joinMatch.Groups["id"].Value;
            var name = joinMatch.Groups["name"].Value;
            _connected[id] = new PlayerInfo(id, name, null, DateTime.UtcNow);
            _log.LogDebug("Player joined: {Name} ({Id})", name, id);
            PlayersChanged?.Invoke(this, EventArgs.Empty);
            return;
        }

        var leaveMatch = LeavePattern.Match(line);
        if (leaveMatch.Success)
        {
            var id = leaveMatch.Groups["id"].Value;
            _connected.TryRemove(id, out _);
            _log.LogDebug("Player left: {Id}", id);
            PlayersChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    private void OnProcessExited(object? sender, ServerProcessExitEventArgs e)
    {
        // Clear the connected player list when the server stops.
        _connected.Clear();
        PlayersChanged?.Invoke(this, EventArgs.Empty);
    }

    // ── Disposal ──────────────────────────────────────────────────────────────

    public void Dispose()
    {
        _process.OutputLineReceived -= OnOutputLine;
        _process.ProcessExited      -= OnProcessExited;
    }
}
