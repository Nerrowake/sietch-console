namespace SietchConsole.Core.Interfaces;

public interface ISetupScriptService
{
    Task RunAsync(
        string scriptPath,
        string workingDirectory,
        Action<string> onOutputLine,
        IProgress<double> progress,
        CancellationToken cancellationToken = default);
}
