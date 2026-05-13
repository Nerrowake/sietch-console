using SietchConsole.Core.Models;

namespace SietchConsole.Core.Interfaces;

/// <summary>
/// Manages connected players, bans, and the allowlist (#158, #159, #160).
/// Player detection is log-based; kick/ban commands are sent via the server process stdin.
/// </summary>
public interface IPlayerManagementService
{
    /// <summary>Players currently detected as connected (updated from log output).</summary>
    IReadOnlyList<PlayerInfo> ConnectedPlayers { get; }

    /// <summary>Raised when the connected player list changes.</summary>
    event EventHandler? PlayersChanged;

    // ── Connected players ────────────────────────────────────────────────────

    Task<IReadOnlyList<PlayerInfo>> GetConnectedPlayersAsync();

    // ── Kick / Ban (#159, #158) ───────────────────────────────────────────────

    /// <summary>Sends a kick command to the server process via stdin.</summary>
    Task KickPlayerAsync(string playerId, string? reason = null);

    /// <summary>
    /// Records a ban in SQLite and sends the ban command to the server.
    /// Pass null <paramref name="duration"/> for a permanent ban.
    /// </summary>
    Task BanPlayerAsync(string playerId, string playerName,
                        string? reason = null, TimeSpan? duration = null);

    Task UnbanPlayerAsync(int banRecordId);

    Task<IReadOnlyList<BanRecord>> GetBanListAsync();

    // ── Allowlist (#160) ─────────────────────────────────────────────────────

    Task<IReadOnlyList<AllowlistEntry>> GetAllowlistAsync();

    Task AddToAllowlistAsync(string steamId, string? displayName = null);

    Task RemoveFromAllowlistAsync(int entryId);
}
