using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using SietchConsole.Core.Interfaces;
using SietchConsole.Core.Models;

namespace Sietch_Console.Services.Profiles;

/// <summary>
/// Singleton service that owns the active battlegroup profile and
/// broadcasts changes to all interested ViewModels (#139).
/// </summary>
public class ActiveProfileService : IActiveProfileService
{
    private readonly IServiceScopeFactory _scopeFactory;

    private List<BattlegroupProfile> _all = [];

    public BattlegroupProfile? Current { get; private set; }

    public IReadOnlyList<BattlegroupProfile> AllProfiles => _all;

    public event EventHandler<BattlegroupProfile?>? ProfileChanged;

    public ActiveProfileService(IServiceScopeFactory scopeFactory)
        => _scopeFactory = scopeFactory;

    // ── Initialise ────────────────────────────────────────────────────────────

    public async Task InitializeAsync()
    {
        using var scope      = _scopeFactory.CreateScope();
        var profileRepo      = scope.ServiceProvider.GetRequiredService<IBattlegroupProfileRepository>();
        var settingsRepo     = scope.ServiceProvider.GetRequiredService<IApplicationSettingsRepository>();

        _all = (await profileRepo.GetAllAsync())
               .OrderBy(p => p.Name)
               .ToList();

        var settings = await settingsRepo.GetAsync();
        if (int.TryParse(settings.LastOpenedBattlegroupId, out var id))
            Current = _all.FirstOrDefault(p => p.Id == id);

        Current ??= _all.FirstOrDefault();
    }

    // ── Switch ────────────────────────────────────────────────────────────────

    public async Task SwitchToAsync(BattlegroupProfile profile)
    {
        if (Current?.Id == profile.Id) return;
        Current = profile;

        using var scope  = _scopeFactory.CreateScope();
        var settingsRepo = scope.ServiceProvider.GetRequiredService<IApplicationSettingsRepository>();
        var settings     = await settingsRepo.GetAsync();
        settings.LastOpenedBattlegroupId = profile.Id.ToString();
        await settingsRepo.SaveAsync(settings);

        FireProfileChanged();
    }

    // ── Create ────────────────────────────────────────────────────────────────

    public async Task<BattlegroupProfile> CreateAsync(string name, string installPath)
    {
        var profile = new BattlegroupProfile
        {
            Name        = name,
            InstallPath = installPath,
        };

        using var scope = _scopeFactory.CreateScope();
        var profileRepo = scope.ServiceProvider.GetRequiredService<IBattlegroupProfileRepository>();
        profile = await profileRepo.AddAsync(profile);

        _all.Add(profile);
        _all = [.. _all.OrderBy(p => p.Name)];

        await SwitchToAsync(profile);
        return profile;
    }

    // ── Delete ────────────────────────────────────────────────────────────────

    public async Task DeleteAsync(BattlegroupProfile profile)
    {
        using var scope = _scopeFactory.CreateScope();
        var profileRepo = scope.ServiceProvider.GetRequiredService<IBattlegroupProfileRepository>();
        await profileRepo.DeleteAsync(profile.Id);

        _all.Remove(profile);

        if (Current?.Id == profile.Id)
        {
            Current = _all.FirstOrDefault();

            if (Current is not null)
            {
                using var scope2     = _scopeFactory.CreateScope();
                var settingsRepo     = scope2.ServiceProvider.GetRequiredService<IApplicationSettingsRepository>();
                var settings         = await settingsRepo.GetAsync();
                settings.LastOpenedBattlegroupId = Current.Id.ToString();
                await settingsRepo.SaveAsync(settings);
            }

            FireProfileChanged();
        }
    }

    // ── Update ────────────────────────────────────────────────────────────────

    public async Task UpdateAsync(BattlegroupProfile profile)
    {
        using var scope = _scopeFactory.CreateScope();
        var profileRepo = scope.ServiceProvider.GetRequiredService<IBattlegroupProfileRepository>();
        profile.UpdatedAt = DateTime.UtcNow;
        await profileRepo.UpdateAsync(profile);

        var idx = _all.FindIndex(p => p.Id == profile.Id);
        if (idx >= 0) _all[idx] = profile;
        _all = [.. _all.OrderBy(p => p.Name)];

        if (Current?.Id == profile.Id)
        {
            Current = profile;
            FireProfileChanged();
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private void FireProfileChanged()
    {
        Application.Current.Dispatcher.InvokeAsync(
            () => ProfileChanged?.Invoke(this, Current));
    }
}
