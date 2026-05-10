using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;

namespace Sietch_Console.Services.Remote;

/// <summary>
/// ASP.NET Core middleware that enforces Bearer token authentication and rate-limits
/// repeated failures by source IP (#151).
///
/// Rules:
/// - GET / is always allowed (serves the login page HTML).
/// - GET /api/events may supply the token via ?token= query param because
///   EventSource does not support custom request headers.
/// - All other /api/* routes require an Authorization: Bearer {token} header.
/// - After 5 failed attempts within 60 s from the same IP, that IP is blocked for 5 minutes.
/// </summary>
public sealed class RemoteAuthMiddleware
{
    /// <summary>Tracks (attempts, window start) per remote IP for rate limiting.</summary>
    private readonly ConcurrentDictionary<string, (int Attempts, DateTime Window)> _failures = new();

    private readonly RequestDelegate _next;
    private readonly string          _token;
    private readonly ILogger         _logger;

    public RemoteAuthMiddleware(RequestDelegate next, string token, ILoggerFactory loggerFactory)
    {
        _next   = next;
        _token  = token;
        _logger = loggerFactory.CreateLogger<RemoteAuthMiddleware>();
    }

    public async Task InvokeAsync(HttpContext ctx)
    {
        var path = ctx.Request.Path.Value ?? "/";

        // Root path always passes through — the page handles auth in the browser.
        if (path == "/")
        {
            await _next(ctx);
            return;
        }

        // Only authenticate /api/* routes.
        if (!path.StartsWith("/api", StringComparison.OrdinalIgnoreCase))
        {
            await _next(ctx);
            return;
        }

        var ip = ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown";

        // ── Rate-limit check ─────────────────────────────────────────────────
        if (_failures.TryGetValue(ip, out var state))
        {
            var age = (DateTime.UtcNow - state.Window).TotalSeconds;
            if (state.Attempts >= 5 && age < 300)
            {
                ctx.Response.StatusCode = 429;
                ctx.Response.Headers.RetryAfter = ((int)(300 - age)).ToString();
                await ctx.Response.WriteAsync("Too Many Requests");
                return;
            }

            // Reset window if 60 s have passed since last failure.
            if (age >= 60)
                _failures.TryRemove(ip, out _);
        }

        // ── Token resolution ─────────────────────────────────────────────────
        // SSE endpoint: EventSource can't send custom headers, so accept ?token= too.
        string? provided = null;
        var authHeader = ctx.Request.Headers.Authorization.FirstOrDefault();
        if (authHeader?.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase) == true)
            provided = authHeader["Bearer ".Length..].Trim();
        else if (path.StartsWith("/api/events", StringComparison.OrdinalIgnoreCase))
            provided = ctx.Request.Query["token"].FirstOrDefault();

        if (provided is not null && provided == _token)
        {
            _failures.TryRemove(ip, out _);   // reset on success
            await _next(ctx);
            return;
        }

        // ── Auth failed ──────────────────────────────────────────────────────
        _logger.LogWarning("Remote auth failure from {IP} on {Path}", ip, path);

        _failures.AddOrUpdate(ip,
            addValue:      (1, DateTime.UtcNow),
            updateValueFactory: (_, prev) =>
                (DateTime.UtcNow - prev.Window).TotalSeconds >= 60
                    ? (1, DateTime.UtcNow)
                    : (prev.Attempts + 1, prev.Window));

        ctx.Response.StatusCode = 401;
        ctx.Response.Headers.WWWAuthenticate = "Bearer";
        await ctx.Response.WriteAsync("Unauthorized");
    }
}
