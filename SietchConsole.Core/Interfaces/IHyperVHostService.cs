using SietchConsole.Core.Models;

namespace SietchConsole.Core.Interfaces;

/// <summary>
/// Manages the registry of Hyper-V hosts (local + remote) and tracks the active host (#153, #154, #156).
/// The local "This machine" host is always present and cannot be removed.
/// </summary>
public interface IHyperVHostService
{
    /// <summary>The built-in local host ("This machine").</summary>
    HyperVHost LocalHost { get; }

    /// <summary>All registered hosts, local first.</summary>
    IReadOnlyList<HyperVHost> AllHosts { get; }

    /// <summary>The currently selected host.</summary>
    HyperVHost ActiveHost { get; }

    /// <summary>Raised on the UI thread when the active host changes.</summary>
    event EventHandler<HyperVHost>? ActiveHostChanged;

    Task InitializeAsync();

    Task<HyperVHost> AddHostAsync(string name, string hostname, int port,
                                  string? username, string? password);

    Task UpdateHostAsync(HyperVHost host, string? newPassword = null);

    Task RemoveHostAsync(HyperVHost host);

    Task SwitchToAsync(HyperVHost host);

    /// <summary>
    /// Attempts a WMI connection to the host's Hyper-V namespace.
    /// Returns (true, null) on success, (false, errorMessage) on failure.
    /// </summary>
    Task<(bool Success, string? ErrorMessage)> TestConnectionAsync(HyperVHost host,
                                                                   CancellationToken ct = default);

    /// <summary>Decrypts and returns the stored password for a host, or null if none.</summary>
    string? GetPassword(HyperVHost host);
}
