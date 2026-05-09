using SietchConsole.Core.Models;

namespace SietchConsole.Core.Interfaces;

public interface ISetupWizardStateRepository
{
    Task<SetupWizardState?> GetActiveAsync();
    Task<SetupWizardState> SaveAsync(SetupWizardState state);
    Task ClearAsync();
}
