namespace SietchConsole.Core.Models;

/// <param name="EndPort">
/// When set, this port entry describes a range from <see cref="Port"/> to
/// <see cref="EndPort"/> (inclusive).  Used for the game-server UDP range 7777–7810.
/// </param>
public sealed record BattlegroupPort(
    string Name,
    int    Port,
    string Protocol,
    string Purpose,
    int?   EndPort = null)
{
    public bool   IsRange    => EndPort.HasValue && EndPort.Value > Port;
    public string PortDisplay => IsRange ? $"{Port}–{EndPort}" : Port.ToString();
}

