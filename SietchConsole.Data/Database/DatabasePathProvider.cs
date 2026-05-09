namespace SietchConsole.Data.Database;

public static class DatabasePathProvider
{
    private const string AppFolderName = "SietchConsole";
    private const string DatabaseFileName = "sietch.db";

    public static string GetDatabasePath()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var folder = Path.Combine(appData, AppFolderName);
        Directory.CreateDirectory(folder);
        return Path.Combine(folder, DatabaseFileName);
    }
}
