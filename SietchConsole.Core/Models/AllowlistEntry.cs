namespace SietchConsole.Core.Models;

/// <summary>An approved player on the server allowlist, stored locally in SQLite (#160).</summary>
public class AllowlistEntry
{
    public int      Id          { get; set; }
    public string   SteamId     { get; set; } = string.Empty;
    public string?  DisplayName { get; set; }
    public DateTime AddedAt     { get; set; } = DateTime.UtcNow;
}
