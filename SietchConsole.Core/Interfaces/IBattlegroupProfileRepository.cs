using SietchConsole.Core.Models;

namespace SietchConsole.Core.Interfaces;

public interface IBattlegroupProfileRepository
{
    Task<IReadOnlyList<BattlegroupProfile>> GetAllAsync();
    Task<BattlegroupProfile?> GetByIdAsync(int id);
    Task<BattlegroupProfile> AddAsync(BattlegroupProfile profile);
    Task UpdateAsync(BattlegroupProfile profile);
    Task DeleteAsync(int id);
}
