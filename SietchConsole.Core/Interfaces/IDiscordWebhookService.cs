namespace SietchConsole.Core.Interfaces;

/// <summary>
/// Sends Discord webhook notifications for server lifecycle events and manual announcements (#170, #171, #172).
/// </summary>
public interface IDiscordWebhookService
{
    /// <summary>Sends a "server started" embed if that notification type is enabled.</summary>
    Task NotifyServerStartedAsync(string serverName);

    /// <summary>Sends a "server stopped" embed if that notification type is enabled.</summary>
    Task NotifyServerStoppedAsync(string serverName);

    /// <summary>Sends a "server crashed" embed if that notification type is enabled.</summary>
    Task NotifyServerCrashedAsync(string serverName, string exitDescription);

    /// <summary>
    /// Sends a plain-text announcement message to the configured webhook.
    /// Used for manual announcements from the Dashboard.
    /// </summary>
    Task SendAnnouncementAsync(string message, string serverName);

    /// <summary>
    /// Posts a test message to the given webhook URL and returns whether it succeeded.
    /// Does not require Discord webhooks to be enabled in settings.
    /// </summary>
    Task<(bool Success, string? Error)> TestWebhookAsync(string webhookUrl);
}
