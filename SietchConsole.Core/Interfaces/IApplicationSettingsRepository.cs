using SietchConsole.Core.Models;

namespace SietchConsole.Core.Interfaces;

public interface IApplicationSettingsRepository
{
    Task<ApplicationSettings> GetAsync();
    Task SaveAsync(ApplicationSettings settings);
}
