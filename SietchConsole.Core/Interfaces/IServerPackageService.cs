namespace SietchConsole.Core.Interfaces;

public interface IServerPackageService
{
    bool IsServerPackagePresent(string installPath);
    string? FindSetupScript(string installPath);
    string? FindBattlegroupScript(string installPath);
    string? LocateInSteamLibraries(IReadOnlyList<string> libraryFolders);
}
