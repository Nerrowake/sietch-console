using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SietchConsole.Core.Interfaces;
using SietchConsole.Core.Models;
using System.Net;
using System.Net.NetworkInformation;
using System.Text.Json;

namespace Sietch_Console.Services.Remote;

/// <summary>
/// Manages the embedded Kestrel web server lifecycle (#152).
///
/// When started:
///   1. Creates a <see cref="WebApplication"/> with Kestrel configured to the requested port.
///   2. Registers <see cref="RemoteAuthMiddleware"/> and maps all API routes via
///      <see cref="RemoteApiEndpoints"/>.
///   3. Subscribes to <see cref="IServerProcessService.OutputLineReceived"/> and
///      periodically polls server status to broadcast SSE events to connected clients.
/// </summary>
public sealed class RemoteManagementService : IRemoteManagementService, IAsyncDisposable
{
    private readonly IServiceProvider                _parentServices;
    private readonly ILogger<RemoteManagementService> _logger;

    private WebApplication?       _webApp;
    private CancellationTokenSource? _broadcastCts;
    private SseHub?               _sseHub;

    // ── IRemoteManagementService ─────────────────────────────────────────────

    public bool    IsRunning  { get; private set; }
    public int     Port       { get; private set; }
    public string? ListenUrl  { get; private set; }

    public event EventHandler? StatusChanged;

    public RemoteManagementService(IServiceProvider parentServices,
                                   ILogger<RemoteManagementService> logger)
    {
        _parentServices = parentServices;
        _logger         = logger;
    }

    // ── Start ─────────────────────────────────────────────────────────────────

    public async Task StartAsync(int port, string token, CancellationToken ct = default)
    {
        if (IsRunning) return;

        _logger.LogInformation("Remote management starting on port {Port}", port);

        _sseHub = new SseHub();

        // ── Build the web application ─────────────────────────────────────────
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            Args = Array.Empty<string>()
        });

        // Silence Kestrel's own console logging; our WPF logger handles everything.
        builder.Logging.ClearProviders();
        builder.Logging.AddProvider(
            _parentServices.GetRequiredService<ILoggerFactory>()
                           .CreateLogger<RemoteManagementService>() is { } l
                ? new SingleLoggerProvider(l)
                : NullLoggerProvider.Instance);

        // Configure Kestrel to listen on all interfaces so LAN clients can reach it.
        builder.WebHost.ConfigureKestrel(kestrel =>
        {
            kestrel.Listen(IPAddress.Any, port);
        });

        // Forward required services from the parent WPF DI container.
        builder.Services.AddSingleton(_sseHub);
        builder.Services.AddSingleton(token);   // used by auth middleware as a named string

        var app = builder.Build();

        // Auth middleware — must be first.
        app.Use(async (ctx, next) =>
        {
            var middleware = new RemoteAuthMiddleware(
                next, token, _parentServices.GetRequiredService<ILoggerFactory>());
            await middleware.InvokeAsync(ctx);
        });

        // Map all API routes.
        RemoteApiEndpoints.MapAll(app, _parentServices, _sseHub);

        // ── Start the web application ─────────────────────────────────────────
        try
        {
            await app.StartAsync(ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start remote management web server on port {Port}", port);
            await app.DisposeAsync();
            _sseHub = null;
            return;
        }

        _webApp = app;
        Port    = port;
        IsRunning = true;
        ListenUrl = BuildListenUrl(port);

        _logger.LogInformation("Remote management available at {Url}", ListenUrl);

        // ── Subscribe to server output for live SSE log feed ──────────────────
        var processService = _parentServices.GetRequiredService<IServerProcessService>();
        processService.OutputLineReceived += OnServerOutputLine;

        // ── Start background status broadcast ─────────────────────────────────
        _broadcastCts = new CancellationTokenSource();
        _ = Task.Run(() => BroadcastStatusLoopAsync(_broadcastCts.Token));

        RaiseStatusChanged();
    }

    // ── Stop ──────────────────────────────────────────────────────────────────

    public async Task StopAsync(CancellationToken ct = default)
    {
        if (!IsRunning || _webApp is null) return;

        _logger.LogInformation("Remote management stopping");

        // Cancel background broadcast loop.
        await (_broadcastCts?.CancelAsync() ?? Task.CompletedTask);
        _broadcastCts?.Dispose();
        _broadcastCts = null;

        // Unsubscribe from server process events.
        var processService = _parentServices.GetRequiredService<IServerProcessService>();
        processService.OutputLineReceived -= OnServerOutputLine;

        await _webApp.StopAsync(ct);
        await _webApp.DisposeAsync();
        _webApp   = null;
        _sseHub   = null;
        IsRunning = false;
        Port      = 0;
        ListenUrl = null;

        RaiseStatusChanged();
        _logger.LogInformation("Remote management stopped");
    }

    // ── IAsyncDisposable ──────────────────────────────────────────────────────

    public async ValueTask DisposeAsync()
    {
        await StopAsync();
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    private void OnServerOutputLine(object? sender, string line)
    {
        if (_sseHub is null) return;
        var entry   = _parentServices.GetRequiredService<ILogFileService>().ParseLine(line);
        var level   = entry?.Severity.ToString() ?? "Info";
        var message = entry?.Message ?? line;
        var ts      = entry is not null && entry.Timestamp != default
                          ? entry.Timestamp.ToString("HH:mm:ss")
                          : null;
        _sseHub.Broadcast("log", System.Text.Json.JsonSerializer.Serialize(
            new { timestamp = ts, level, message },
            new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase }));
    }

    private async Task BroadcastStatusLoopAsync(CancellationToken ct)
    {
        var control  = _parentServices.GetRequiredService<IBattlegroupControlService>();
        var profiles = _parentServices.GetRequiredService<IActiveProfileService>();
        var timer    = new PeriodicTimer(TimeSpan.FromSeconds(3));

        BattlegroupRuntimeStatus? lastStatus = null;

        try
        {
            while (await timer.WaitForNextTickAsync(ct))
            {
                if (_sseHub?.ClientCount == 0) continue;

                var profile = profiles.Current;
                if (profile is null) continue;

                var status = await control.GetStatusAsync(profile);
                if (status != lastStatus)
                {
                    lastStatus = status;
                    _sseHub?.Broadcast("status",
                        System.Text.Json.JsonSerializer.Serialize(
                            new { status = status.ToString(), uptimeSeconds = (int?)null, playerCount = 0 },
                            new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase }));
                }
            }
        }
        catch (OperationCanceledException) { }
    }

    private static string BuildListenUrl(int port)
    {
        // Return the first non-loopback IPv4 address on the host so the URL is usable
        // from another device on the LAN.
        foreach (var ni in NetworkInterface.GetAllNetworkInterfaces())
        {
            if (ni.OperationalStatus != OperationalStatus.Up) continue;
            if (ni.NetworkInterfaceType is NetworkInterfaceType.Loopback) continue;

            foreach (var addr in ni.GetIPProperties().UnicastAddresses)
            {
                if (addr.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                    return $"http://{addr.Address}:{port}";
            }
        }

        return $"http://localhost:{port}";
    }

    private void RaiseStatusChanged() =>
        StatusChanged?.Invoke(this, EventArgs.Empty);

    // ── Private ILoggerProvider shim ─────────────────────────────────────────

    private sealed class SingleLoggerProvider : ILoggerProvider
    {
        private readonly ILogger _logger;
        public SingleLoggerProvider(ILogger logger) => _logger = logger;
        public ILogger CreateLogger(string _) => _logger;
        public void Dispose() { }
    }

    private sealed class NullLoggerProvider : ILoggerProvider
    {
        public static readonly NullLoggerProvider Instance = new();
        public ILogger CreateLogger(string _) => Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance;
        public void Dispose() { }
    }
}
