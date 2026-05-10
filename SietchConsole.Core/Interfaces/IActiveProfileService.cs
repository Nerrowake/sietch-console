using SietchConsole.Core.Models;

namespace SietchConsole.Core.Interfaces;

/// <summary>
/// Singleton service that tracks the active battlegroup profile and
/// broadcasts a <see cref="ProfileChanged"/> event when the user switches
/// profiles (#139).  All ViewModels that display per-profile data should
/// subscribe to this event and reload their state on receipt.
/// </summary>
public interface IActiveProfileService
{
    /// <summary>The currently active profile, or <c>null</c> if none exists.</summary>
    BattlegroupProfile? Current { get; }

    /// <summary>All profiles known to the application, ordered by name.</summary>
    IReadOnlyList<BattlegroupProfile> AllProfiles { get; }

    /// <summary>
    /// Fired on the UI thread whenever the active profile changes.
    /// The argument is the new active profile (may be <c>null</c>).
    /// </summary>
    event EventHandler<BattlegroupProfile?> ProfileChanged;

    /// <summary>Load all profiles from the database and set the active one.</summary>
    Task InitializeAsync();

    /// <summary>Switch the active profile and persist the choice to settings.</summary>
    Task SwitchToAsync(BattlegroupProfile profile);

    /// <summary>Create a new profile, add it to the database, and switch to it.</summary>
    Task<BattlegroupProfile> CreateAsync(string name, string installPath);

    /// <summary>
    /// Delete a profile and its backup records from the database.
    /// If the deleted profile was active, switches to another available profile (or null).
    /// </summary>
    Task DeleteAsync(BattlegroupProfile profile);

    /// <summary>Persist in-place edits to a profile (name, installPath, etc.).</summary>
    Task UpdateAsync(BattlegroupProfile profile);
}
