using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SietchConsole.Core.Interfaces;
using SietchConsole.Core.Models;

namespace Sietch_Console.Services.Discord;

/// <summary>
/// Sends Discord webhook embeds for server lifecycle events and manual announcements (#170, #171, #172).
/// Subscribes to IServerProcessService events in the constructor; no external initializer call is needed.
/// </summary>
public sealed class DiscordWebhookService : IDiscordWebhookService, IDisposable
{
    // Discord embed accent colors (integer RGB).
    private const int ColorGreen  = 0x57F287;   // server started
    private const int ColorOrange = 0xFEE75C;   // server stopped normally
    private const int ColorRed    = 0xED4245;   // server crashed
    private const int ColorBlue   = 0x5865F2;   // announcements

    private readonly IServiceScopeFactory            _scopeFactory;
    private readonly IServerProcessService           _process;
    private readonly IActiveProfileService           _profiles;
    private readonly ILogger<DiscordWebhookService>  _log;
    private readonly HttpClient                      _http;

    public DiscordWebhookService(
        IServiceScopeFactory           scopeFactory,
        IServerProcessService          process,
        IActiveProfileService          profiles,
        ILogger<DiscordWebhookService> log)
    {
        _scopeFactory = scopeFactory;
        _process      = process;
        _profiles     = profiles;
        _log          = log;

        _http = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
        _http.DefaultRequestHeaders.Add("User-Agent", "SietchConsole-Discord/1.0");

        _process.ServerStarted += OnServerStarted;
        _process.ProcessExited += OnProcessExited;
    }

    // ── IDiscordWebhookService ────────────────────────────────────────────────

    public Task NotifyServerStartedAsync(string serverName) =>
        FireAndForgetAsync(ColorGreen,
            title:       "✅ Server Online",
            description: $"**{serverName}** is now online and accepting connections.",
            footer:      "Sietch Console");

    public Task NotifyServerStoppedAsync(string serverName) =>
        FireAndForgetAsync(ColorOrange,
            title:       "🟡 Server Offline",
            description: $"**{serverName}** has been stopped.",
            footer:      "Sietch Console");

    public Task NotifyServerCrashedAsync(string serverName, string exitDescription) =>
        FireAndForgetAsync(ColorRed,
            title:       "🔴 Server Crashed",
            description: $"**{serverName}** exited unexpectedly.\n```\n{exitDescription}\n```",
            footer:      "Sietch Console");

    public Task SendAnnouncementAsync(string message, string serverName) =>
        FireAndForgetAsync(ColorBlue,
            title:       $"📢 {serverName}",
            description: message,
            footer:      "Sietch Console");

    public async Task<(bool Success, string? Error)> TestWebhookAsync(string webhookUrl)
    {
        if (string.IsNullOrWhiteSpace(webhookUrl))
            return (false, "Webhook URL is empty.");

        try
        {
            var payload = BuildPayload(
                color:       ColorBlue,
                title:       "🔔 Test Notification",
                description: "Sietch Console is connected to this webhook.",
                footer:      "Sietch Console");

            using var response = await _http.PostAsJsonAsync(webhookUrl, payload);

            if (response.IsSuccessStatusCode)
                return (true, null);

            var body = await response.Content.ReadAsStringAsync();
            return (false, $"HTTP {(int)response.StatusCode}: {body}");
        }
        catch (Exception ex)
        {
            return (false, ex.Message);
        }
    }

    // ── Event handlers ────────────────────────────────────────────────────────

    private void OnServerStarted(object? sender, EventArgs e)
    {
        var serverName = _profiles.Current?.Name ?? "Server";
        _ = NotifyIfEnabledAsync(
            predicate:    s => s.DiscordNotifyServerStart,
            notify:       () => NotifyServerStartedAsync(serverName));
    }

    private void OnProcessExited(object? sender, ServerProcessExitEventArgs e)
    {
        var serverName = _profiles.Current?.Name ?? "Server";

        if (e.WasExpected)
        {
            _ = NotifyIfEnabledAsync(
                predicate: s => s.DiscordNotifyServerStop,
                notify:    () => NotifyServerStoppedAsync(serverName));
        }
        else
        {
            _ = NotifyIfEnabledAsync(
                predicate: s => s.DiscordNotifyServerCrash,
                notify:    () => NotifyServerCrashedAsync(serverName, e.Description));
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Loads settings, checks the master enable flag and the per-event predicate,
    /// then calls <paramref name="notify"/> if both are true.
    /// </summary>
    private async Task NotifyIfEnabledAsync(
        Func<ApplicationSettings, bool> predicate,
        Func<Task>                      notify)
    {
        try
        {
            ApplicationSettings settings;
            using (var scope = _scopeFactory.CreateScope())
            {
                var repo = scope.ServiceProvider.GetRequiredService<IApplicationSettingsRepository>();
                settings = await repo.GetAsync();
            }

            if (!settings.DiscordWebhookEnabled
                || string.IsNullOrWhiteSpace(settings.DiscordWebhookUrl)
                || !predicate(settings))
                return;

            await notify();
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "Discord notification check failed.");
        }
    }

    /// <summary>
    /// Posts an embed to the configured webhook URL.
    /// Retries once on HTTP 5xx or network timeout; drops on second failure.
    /// </summary>
    private async Task FireAndForgetAsync(int color, string title, string description, string footer)
    {
        ApplicationSettings settings;
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var repo = scope.ServiceProvider.GetRequiredService<IApplicationSettingsRepository>();
            settings = await repo.GetAsync();
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "Discord: failed to load settings.");
            return;
        }

        if (!settings.DiscordWebhookEnabled
            || string.IsNullOrWhiteSpace(settings.DiscordWebhookUrl))
            return;

        var payload = BuildPayload(color, title, description, footer);

        for (int attempt = 1; attempt <= 2; attempt++)
        {
            try
            {
                using var response = await _http.PostAsJsonAsync(settings.DiscordWebhookUrl, payload);

                if (response.IsSuccessStatusCode)
                    return;

                // Discord rate-limit or server error — only retry on 5xx.
                if ((int)response.StatusCode < 500)
                {
                    var body = await response.Content.ReadAsStringAsync();
                    _log.LogWarning("Discord webhook returned {Status} on attempt {Attempt}: {Body}",
                        (int)response.StatusCode, attempt, body);
                    return;   // client error — do not retry
                }

                _log.LogWarning("Discord webhook returned {Status} on attempt {Attempt}; retrying.",
                    (int)response.StatusCode, attempt);
            }
            catch (Exception ex)
            {
                _log.LogWarning(ex, "Discord webhook POST failed on attempt {Attempt}.", attempt);
                if (attempt == 2) return;   // give up after second failure
            }

            await Task.Delay(2_000);   // brief pause before retry
        }
    }

    private static object BuildPayload(int color, string title, string description, string footer) => new
    {
        embeds = new[]
        {
            new
            {
                title,
                description,
                color,
                footer    = new { text = footer },
                timestamp = DateTime.UtcNow.ToString("o"),
            },
        },
    };

    // ── Disposal ──────────────────────────────────────────────────────────────

    public void Dispose()
    {
        _process.ServerStarted -= OnServerStarted;
        _process.ProcessExited -= OnProcessExited;
        _http.Dispose();
    }
}
