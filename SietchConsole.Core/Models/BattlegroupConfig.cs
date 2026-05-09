namespace SietchConsole.Core.Models;

/// <summary>Strongly typed model for known battlegroup server configuration settings.</summary>
public class BattlegroupConfig
{
    // ── Server identity ──────────────────────────────────────────────────
    public string ServerName      { get; set; } = string.Empty;
    public int    MaxPlayers      { get; set; } = 20;
    public string ServerPassword  { get; set; } = string.Empty;
    public string AdminPassword   { get; set; } = string.Empty;

    // ── Network ──────────────────────────────────────────────────────────
    public int GamePort  { get; set; } = 7777;

    // ── Gameplay ─────────────────────────────────────────────────────────
    public bool  PvPEnabled            { get; set; } = false;
    public float DayLengthMultiplier   { get; set; } = 1.0f;
    public float NightLengthMultiplier { get; set; } = 1.0f;
    public int   MaxTribeMemberCount   { get; set; } = 10;
    public float ResourceHarvestingRate { get; set; } = 1.0f;
    public float XpMultiplier          { get; set; } = 1.0f;
}
