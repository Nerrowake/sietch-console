namespace SietchConsole.Data.Database;

public class DatabaseInitializerService
{
    private readonly SietchConsoleDbContext _db;

    public DatabaseInitializerService(SietchConsoleDbContext db) => _db = db;

    public async Task InitializeAsync()
    {
        await _db.Database.EnsureCreatedAsync();
    }
}
