namespace SietchConsole.Core.Models;

/// <summary>A player currently connected to the server, detected from log output (#159).</summary>
public sealed record PlayerInfo(
    string   PlayerId,
    string   PlayerName,
    string?  SteamId,
    DateTime JoinedAt);
