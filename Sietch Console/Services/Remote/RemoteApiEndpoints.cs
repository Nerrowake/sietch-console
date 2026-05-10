using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using SietchConsole.Core.Interfaces;
using SietchConsole.Core.Models;
using System.Reflection;
using System.Text;
using System.Text.Json;

namespace Sietch_Console.Services.Remote;

/// <summary>
/// Maps all remote management API routes onto a <see cref="WebApplication"/> (#149).
///
/// Routes:
///   GET  /                         → dashboard.html (always accessible)
///   GET  /api/status               → { status, uptimeSeconds, playerCount }
///   POST /api/control/start        → 202 Accepted
///   POST /api/control/stop         → 202 Accepted
///   POST /api/control/restart      → 202 Accepted
///   GET  /api/logs?lines=50        → [{ timestamp, level, message }]
///   GET  /api/events               → SSE stream (text/event-stream)
/// </summary>
public static class RemoteApiEndpoints
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public static void MapAll(WebApplication app, IServiceProvider parentServices, SseHub sseHub)
    {
        var control = parentServices.GetRequiredService<IBattlegroupControlService>();
        var profiles = parentServices.GetRequiredService<IActiveProfileService>();
        var logFile  = parentServices.GetRequiredService<ILogFileService>();

        // ── Dashboard HTML ───────────────────────────────────────────────────
        app.MapGet("/", () =>
        {
            var asm    = Assembly.GetExecutingAssembly();
            var stream = asm.GetManifestResourceStream("Sietch_Console.Resources.dashboard.html");
            if (stream is null) return Results.NotFound("dashboard.html not embedded");
            return Results.Stream(stream, "text/html; charset=utf-8");
        });

        // ── Status ───────────────────────────────────────────────────────────
        app.MapGet("/api/status", async () =>
        {
            var profile = profiles.Current;
            if (profile is null)
                return Results.Ok(new { status = "Offline", uptimeSeconds = (int?)null, playerCount = 0 });

            var status = await control.GetStatusAsync(profile);
            return Results.Ok(new
            {
                status        = status.ToString(),
                uptimeSeconds = (int?)null,  // uptime tracked in RemoteManagementService via SSE
                playerCount   = 0            // player count not yet available from server API
            });
        });

        // ── Control ──────────────────────────────────────────────────────────
        app.MapPost("/api/control/start", async () =>
        {
            var profile = profiles.Current;
            if (profile is null) return Results.BadRequest("No active profile");
            _ = Task.Run(() => control.StartAsync(profile));
            return Results.Accepted();
        });

        app.MapPost("/api/control/stop", async () =>
        {
            var profile = profiles.Current;
            if (profile is null) return Results.BadRequest("No active profile");
            _ = Task.Run(() => control.StopAsync(profile));
            return Results.Accepted();
        });

        app.MapPost("/api/control/restart", async () =>
        {
            var profile = profiles.Current;
            if (profile is null) return Results.BadRequest("No active profile");
            _ = Task.Run(() => control.RestartAsync(profile));
            return Results.Accepted();
        });

        // ── Log tail ─────────────────────────────────────────────────────────
        app.MapGet("/api/logs", async (int lines = 50) =>
        {
            var profile = profiles.Current;
            if (profile is null) return Results.Ok(Array.Empty<object>());

            var files = logFile.GetLogFiles(profile);
            if (files.Count == 0) return Results.Ok(Array.Empty<object>());

            var latest  = files[^1];
            var entries = await logFile.ReadAllAsync(latest);
            var tail    = entries.TakeLast(Math.Clamp(lines, 1, 500))
                                 .Select(e => new
                                 {
                                     timestamp = e.Timestamp != default ? e.Timestamp.ToString("HH:mm:ss") : null,
                                     level     = e.Severity.ToString(),
                                     message   = e.Message
                                 });
            return Results.Ok(tail);
        });

        // ── SSE event stream ──────────────────────────────────────────────────
        app.MapGet("/api/events", async (HttpContext ctx) =>
        {
            ctx.Response.Headers.ContentType  = "text/event-stream";
            ctx.Response.Headers.CacheControl = "no-cache";
            ctx.Response.Headers.Connection   = "keep-alive";
            await ctx.Response.Body.FlushAsync();

            var id     = sseHub.Subscribe();
            var reader = sseHub.GetReader(id);
            var ct     = ctx.RequestAborted;

            try
            {
                // Send initial status immediately so the client doesn't wait for a broadcast.
                var profile = profiles.Current;
                if (profile is not null)
                {
                    var status = await control.GetStatusAsync(profile);
                    var frame  = BuildStatusFrame(status);
                    var bytes  = Encoding.UTF8.GetBytes(frame);
                    await ctx.Response.Body.WriteAsync(bytes, ct);
                    await ctx.Response.Body.FlushAsync(ct);
                }

                if (reader is null) return;

                await foreach (var frame in reader.ReadAllAsync(ct))
                {
                    var bytes = Encoding.UTF8.GetBytes(frame);
                    await ctx.Response.Body.WriteAsync(bytes, ct);
                    await ctx.Response.Body.FlushAsync(ct);
                }
            }
            catch (OperationCanceledException) { /* client disconnected */ }
            finally
            {
                sseHub.Unsubscribe(id);
            }
        });
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    internal static string BuildStatusFrame(BattlegroupRuntimeStatus status, int uptimeSec = 0, int playerCount = 0)
    {
        var json = JsonSerializer.Serialize(new
        {
            status        = status.ToString(),
            uptimeSeconds = uptimeSec,
            playerCount
        }, JsonOpts);
        return $"event: status\ndata: {json}\n\n";
    }

    internal static string BuildLogFrame(string? timestamp, string level, string message)
    {
        var json = JsonSerializer.Serialize(new { timestamp, level, message }, JsonOpts);
        return $"event: log\ndata: {json}\n\n";
    }
}
