namespace SietchConsole.Core.Models;

/// <summary>Result of a command executed over SSH.</summary>
public sealed record SshCommandResult(int ExitCode, string Output, string Error)
{
    public bool Success => ExitCode == 0;
}
