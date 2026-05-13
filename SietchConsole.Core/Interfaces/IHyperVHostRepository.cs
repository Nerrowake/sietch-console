using SietchConsole.Core.Models;

namespace SietchConsole.Core.Interfaces;

public interface IHyperVHostRepository
{
    Task<IReadOnlyList<HyperVHost>> GetAllAsync();
    Task<HyperVHost?> GetByIdAsync(string id);
    Task AddAsync(HyperVHost host);
    Task UpdateAsync(HyperVHost host);
    Task DeleteAsync(string id);
}
