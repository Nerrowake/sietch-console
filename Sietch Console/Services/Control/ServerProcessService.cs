using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;
using SietchConsole.Core.Interfaces;
using SietchConsole.Core.Models;

namespace Sietch_Console.Services.Control;

/// <summary>
/// Manages the Dune: Awakening dedicated server process on the host machine.
/// Streams stdout/stderr to subscribers, detects the server-ready state from
/// log output, and handles both graceful and forced shutdown.
/// </summary>
public sealed class ServerProcessService : IServerProcessService, IDisposable
{
    // ── Server executable discovery ──────────────────────────────────────────

    // Known executable names for the Dune: Awakening dedicated server.
    // TODO: Verify final name against Funcom's dedicated-server documentation before v1.0.
    private static readonly string[] ServerExeNames =
    [
        "DedicatedServer.exe",
        "DuneServer.exe",
        "BattlegroupServer.exe",
        "start_server.bat",
        "startserver.bat",
    ];

    // Subdirectories beneath InstallPath to search for the server executable.
    private static readonly string[] SearchSubdirs =
    [
        "",
        "Binaries",
        @"Binaries\Win64",
        "bin",
        "server",
    ];

    // ── Server-ready detection ───────────────────────────────────────────────

    // Patterns that indicate the server has finished initialising and is accepting connections.
    // UE5-based games typically log a net listener line; we match common variants.
    private static readonly Regex[] ReadyPatterns =
    [
        new(@"(?i)(listening on port|net:\s*server\s*listening|ready to accept|serverready|game port \d+ opened)",
            RegexOptions.Compiled),
        new(@"(?i)(LogLoad.*Took.*LoadMap|server is ready|accepting connections|startup complete)",
            RegexOptions.Compiled),
    ];

    // ── State ────────────────────────────────────────────────────────────────

    private readonly object _lock = new();
    private Process?        _process;
    private volatile bool   _isServerReady;
    private volatile bool   _wasStopRequested;
    private int?            _lastExitCode;
    private string?         _lastExitDescription;

    public bool    IsRunning           => _process is { HasExited: false };
    public bool    IsServerReady       => _isServerReady;
    public int?    LastExitCode        => _lastExitCode;
    public string? LastExitDescription => _lastExitDescription;

    // ── Events ───────────────────────────────────────────────────────────────

    public event EventHandler<string>?                    OutputLineReceived;
    public event EventHandler<ServerProcessExitEventArgs>? ProcessExited;

    // ── Start (#128) ─────────────────────────────────────────────────────────

    public Task StartAsync(BattlegroupProfile profile, CancellationToken ct = default)
    {
        lock (_lock)
        {
            if (IsRunning)
                return Task.CompletedTask;

            var exePath = FindServerExe(profile.InstallPath);
            if (exePath is null)
                throw new InvalidOperationException(
                    $"No server executable was found in '{profile.InstallPath}'. " +
                    "Run the Setup Wizard to install the server files first.");

            _isServerReady    = false;
            _wasStopRequested = false;

            var isBat = Path.GetExtension(exePath)
                            .Equals(".bat", StringComparison.OrdinalIgnoreCase);

            var psi = new ProcessStartInfo
            {
                FileName               = isBat ? "cmd.exe" : exePath,
                Arguments              = isBat ? $"/c \"{exePath}\"" : string.Empty,
                WorkingDirectory       = profile.InstallPath,
                UseShellExecute        = false,
                RedirectStandardOutput = true,
                RedirectStandardError  = true,
                RedirectStandardInput  = true,   // needed for graceful stdin-quit
                CreateNoWindow         = true,
            };

            var p = new Process { StartInfo = psi, EnableRaisingEvents = true };

            p.OutputDataReceived += OnOutputLine;
            p.ErrorDataReceived  += OnErrorLine;
            p.Exited             += OnProcessExited;

            p.Start();
            p.BeginOutputReadLine();
            p.BeginErrorReadLine();

            _process = p;
        }

        return Task.CompletedTask;
    }

    // ── Stop (#132) ──────────────────────────────────────────────────────────

    public async Task StopAsync(TimeSpan timeout, CancellationToken ct = default)
    {
        Process? p;
        lock (_lock)
        {
            p = _process;
            if (p is null || p.HasExited)
                return;

            _wasStopRequested = true;
        }

        // 1. Try graceful: write "quit" to stdin (many game servers honour this).
        try
        {
            if (!p.HasExited)
                await p.StandardInput.WriteLineAsync("quit").WaitAsync(ct);
        }
        catch { /* stdin may be closed or unsupported — continue */ }

        // 2. Try graceful: send WM_CLOSE to the main window (works for windowed apps).
        try
        {
            if (!p.HasExited)
                p.CloseMainWindow();
        }
        catch { }

        // 3. Wait up to <timeout> for natural exit.
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct);
        linked.CancelAfter(timeout);
        try
        {
            await p.WaitForExitAsync(linked.Token);
        }
        catch (OperationCanceledException) { }

        // 4. Force-kill if still alive.
        try
        {
            if (!p.HasExited)
                p.Kill(entireProcessTree: true);
        }
        catch { }
    }

    // ── Stdin command (#158) ────────────────────────────────────────────────

    public async Task SendCommandAsync(string command)
    {
        Process? p;
        lock (_lock) { p = _process; }
        if (p is null || p.HasExited) return;
        try
        {
            await p.StandardInput.WriteLineAsync(command);
        }
        catch { /* stdin closed or process gone — no-op */ }
    }

    // ── Output handling (#129) ───────────────────────────────────────────────

    private void OnOutputLine(object sender, DataReceivedEventArgs e)
    {
        if (e.Data is null) return;
        CheckReadyPatterns(e.Data);
        OutputLineReceived?.Invoke(this, e.Data);
    }

    private void OnErrorLine(object sender, DataReceivedEventArgs e)
    {
        if (e.Data is null) return;
        // Prefix stderr lines so the Logs view can distinguish them.
        OutputLineReceived?.Invoke(this, $"[stderr] {e.Data}");
    }

    // ── Server-ready detection (#130) ────────────────────────────────────────

    private void CheckReadyPatterns(string line)
    {
        if (_isServerReady) return;
        foreach (var pattern in ReadyPatterns)
        {
            if (pattern.IsMatch(line))
            {
                _isServerReady = true;
                return;
            }
        }
    }

    // ── Exit handling (#131) ─────────────────────────────────────────────────

    private void OnProcessExited(object? sender, EventArgs e)
    {
        var p = _process;
        if (p is null) return;

        int code;
        try   { code = p.ExitCode; }
        catch { code = -1; }

        var wasExpected = _wasStopRequested;
        var desc        = DescribeExitCode(code, wasExpected);

        _lastExitCode        = code;
        _lastExitDescription = desc;
        _isServerReady       = false;

        ProcessExited?.Invoke(this, new ServerProcessExitEventArgs
        {
            ExitCode    = code,
            Description = desc,
            WasExpected = wasExpected,
        });
    }

    private static string DescribeExitCode(int code, bool wasExpected)
    {
        if (wasExpected)
            return "Server stopped normally.";

        return (uint)code switch
        {
            0            => "Server exited cleanly.",
            0xC0000005u  => "Server crashed: access violation (0xC0000005). Check logs for details.",
            0xC0000FD0u  => "Server ran out of stack space (0xC0000FD). Consider increasing VM memory.",
            0xC0000409u  => "Server crashed: security check failure (0xC0000409).",
            _            => code is -1
                ? "Server was force-killed."
                : $"Server exited unexpectedly (exit code {code}). Review the log output for details.",
        };
    }

    // ── Executable search ────────────────────────────────────────────────────

    private static string? FindServerExe(string installPath)
    {
        if (string.IsNullOrWhiteSpace(installPath) || !Directory.Exists(installPath))
            return null;

        foreach (var sub in SearchSubdirs)
        {
            var dir = string.IsNullOrEmpty(sub)
                ? installPath
                : Path.Combine(installPath, sub);

            if (!Directory.Exists(dir)) continue;

            foreach (var name in ServerExeNames)
            {
                var path = Path.Combine(dir, name);
                if (File.Exists(path))
                    return path;
            }
        }

        return null;
    }

    // ── Disposal ─────────────────────────────────────────────────────────────

    public void Dispose()
    {
        try { _process?.Kill(entireProcessTree: true); } catch { }
        _process?.Dispose();
    }
}
