namespace SietchConsole.Core.Models;

public class DiagnosticsResult
{
    public int Id { get; set; }

    public string CheckName { get; set; } = string.Empty;

    /// <summary>Pass, Warning, Failure, or Unknown.</summary>
    public string Severity { get; set; } = "Unknown";

    public string Title { get; set; } = string.Empty;

    public string TechnicalMessage { get; set; } = string.Empty;

    public string? FriendlyMessage { get; set; }

    public string? RecommendedAction { get; set; }

    public int? BattlegroupProfileId { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
