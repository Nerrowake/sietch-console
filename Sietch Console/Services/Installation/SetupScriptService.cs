using SietchConsole.Core.Interfaces;
using System.Diagnostics;
using System.IO;

namespace Sietch_Console.Services.Installation;

public class SetupScriptService : ISetupScriptService
{
    // Output lines that indicate the script completed successfully
    private static readonly string[] SuccessMarkers =
    [
        "Setup complete",
        "Installation successful",
        "Battlegroup ready",
        "Done.",
    ];

    // Output lines that indicate a failure
    private static readonly string[] FailureMarkers =
    [
        "Error:",
        "FAILED",
        "Access denied",
        "cannot find",
        "is not recognized",
    ];

    public async Task RunAsync(
        string scriptPath,
        string workingDirectory,
        Action<string> onOutputLine,
        IProgress<double> progress,
        CancellationToken cancellationToken = default)
    {
        if (!File.Exists(scriptPath))
            throw new FileNotFoundException($"Setup script not found: {scriptPath}");

        var psi = new ProcessStartInfo
        {
            FileName = "cmd.exe",
            Arguments = $"/c \"{scriptPath}\"",
            WorkingDirectory = workingDirectory,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
        };

        using var process = new Process { StartInfo = psi, EnableRaisingEvents = true };

        int linesRead = 0;
        bool failed = false;

        process.OutputDataReceived += (_, e) =>
        {
            if (e.Data is null) return;
            onOutputLine(e.Data);
            linesRead++;
            // Heuristic progress: ramp up to 90% based on output volume
            progress.Report(Math.Min(90.0, linesRead * 2.0));

            if (FailureMarkers.Any(m => e.Data.Contains(m, StringComparison.OrdinalIgnoreCase)))
                failed = true;
        };

        process.ErrorDataReceived += (_, e) =>
        {
            if (e.Data is null) return;
            onOutputLine($"[stderr] {e.Data}");
            if (FailureMarkers.Any(m => e.Data.Contains(m, StringComparison.OrdinalIgnoreCase)))
                failed = true;
        };

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        await process.WaitForExitAsync(cancellationToken);

        progress.Report(100.0);

        if (process.ExitCode != 0 || failed)
            throw new InvalidOperationException(
                $"Setup script exited with code {process.ExitCode}. Check the log for details.");
    }
}
