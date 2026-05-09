namespace SietchConsole.Core.Interfaces;

public interface ISteamDetectionService
{
    string? DetectSteamPath();
    bool IsSteamInstalled();
    IReadOnlyList<string> GetLibraryFolders(string steamPath);
}
