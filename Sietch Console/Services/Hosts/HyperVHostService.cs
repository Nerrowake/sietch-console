using System.Management;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SietchConsole.Core.Interfaces;
using SietchConsole.Core.Models;

namespace Sietch_Console.Services.Hosts;

/// <summary>
/// Manages the list of registered Hyper-V hosts and tracks which host is currently
/// active. The local machine is always present as a built-in, non-removable entry.
/// Remote host credentials are encrypted with DPAPI before writing to SQLite (#153).
/// </summary>
public sealed class HyperVHostService : IHyperVHostService, IAsyncDisposable
{
    // Built-in local host (constant Id used as FK from BattlegroupProfile.HostId = null → local).
    private static readonly HyperVHost _localHost = new()
    {
        Id      = "local",
        Name    = "This machine",
        Hostname= "localhost",
        IsLocal = true,
    };

    private readonly IServiceScopeFactory       _scopeFactory;
    private readonly ILogger<HyperVHostService> _log;

    private readonly List<HyperVHost> _hosts = [];
    private readonly SemaphoreSlim    _gate  = new(1, 1);
    private HyperVHost                _activeHost = _localHost;

    // ── IHyperVHostService ────────────────────────────────────────────────────

    public HyperVHost                LocalHost  => _localHost;
    public IReadOnlyList<HyperVHost> AllHosts   => _hosts.AsReadOnly();
    public HyperVHost                ActiveHost => _activeHost;

    public event EventHandler<HyperVHost>? ActiveHostChanged;

    public HyperVHostService(IServiceScopeFactory scopeFactory, ILogger<HyperVHostService> log)
    {
        _scopeFactory = scopeFactory;
        _log          = log;
    }

    public async Task InitializeAsync()
    {
        await _gate.WaitAsync();
        try
        {
            _hosts.Clear();
            _hosts.Add(_localHost);

            using var scope = _scopeFactory.CreateScope();
            var repo   = scope.ServiceProvider.GetRequiredService<IHyperVHostRepository>();
            var remote = await repo.GetAllAsync();
            _hosts.AddRange(remote.Where(h => !h.IsLocal));
        }
        finally { _gate.Release(); }

        _log.LogInformation("HyperVHostService initialised: {Count} host(s) loaded.", _hosts.Count);
    }

    public async Task<HyperVHost> AddHostAsync(
        string name, string hostname, int port, string? username, string? password)
    {
        var host = new HyperVHost
        {
            Id                = Guid.NewGuid().ToString(),
            Name              = name,
            Hostname          = hostname,
            Port              = port,
            Username          = username,
            EncryptedPassword = EncryptPassword(password),
            IsLocal           = false,
            CreatedAt         = DateTime.UtcNow,
        };

        await _gate.WaitAsync();
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var repo = scope.ServiceProvider.GetRequiredService<IHyperVHostRepository>();
            await repo.AddAsync(host);
            _hosts.Add(host);
        }
        finally { _gate.Release(); }

        return host;
    }

    public async Task UpdateHostAsync(HyperVHost host, string? newPassword = null)
    {
        ArgumentNullException.ThrowIfNull(host);
        if (host.IsLocal)
            throw new InvalidOperationException("The local host entry cannot be modified.");

        if (newPassword is not null)
            host.EncryptedPassword = EncryptPassword(newPassword);

        await _gate.WaitAsync();
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var repo = scope.ServiceProvider.GetRequiredService<IHyperVHostRepository>();
            await repo.UpdateAsync(host);

            var idx = _hosts.FindIndex(h => h.Id == host.Id);
            if (idx >= 0) _hosts[idx] = host;
        }
        finally { _gate.Release(); }
    }

    public async Task RemoveHostAsync(HyperVHost host)
    {
        ArgumentNullException.ThrowIfNull(host);
        if (host.IsLocal)
            throw new InvalidOperationException("The local host entry cannot be removed.");

        await _gate.WaitAsync();
        try
        {
            if (_activeHost.Id == host.Id)
            {
                _activeHost = _localHost;
                ActiveHostChanged?.Invoke(this, _localHost);
            }

            using var scope = _scopeFactory.CreateScope();
            var repo = scope.ServiceProvider.GetRequiredService<IHyperVHostRepository>();
            await repo.DeleteAsync(host.Id);
            _hosts.RemoveAll(h => h.Id == host.Id);
        }
        finally { _gate.Release(); }
    }

    public Task SwitchToAsync(HyperVHost host)
    {
        ArgumentNullException.ThrowIfNull(host);
        var target = _hosts.FirstOrDefault(h => h.Id == host.Id)
            ?? throw new InvalidOperationException($"Host '{host.Id}' is not registered.");

        _activeHost = target;
        ActiveHostChanged?.Invoke(this, target);
        return Task.CompletedTask;
    }

    public async Task<(bool Success, string? ErrorMessage)> TestConnectionAsync(
        HyperVHost host, CancellationToken ct = default)
    {
        if (host.IsLocal)
            return (true, null);

        return await Task.Run(() =>
        {
            try
            {
                var opts = BuildConnectionOptions(host);
                var path = $"\\\\{host.Hostname}\\root\\virtualization\\v2";
                var scope = new ManagementScope(path, opts);
                scope.Connect();

                using var searcher = new ManagementObjectSearcher(
                    scope,
                    new ObjectQuery(
                        "SELECT Name FROM Msvm_ComputerSystem WHERE Caption = 'Virtual Machine'"));
                _ = searcher.Get().Count; // just probe — we don't care how many
                return (true, (string?)null);
            }
            catch (ManagementException ex) when (ex.ErrorCode == ManagementStatus.AccessDenied)
            {
                return (false, "Access denied — check credentials.");
            }
            catch (Exception ex)
            {
                return (false, ex.Message);
            }
        }, ct);
    }

    public string? GetPassword(HyperVHost host)
    {
        if (host.EncryptedPassword is not { Length: > 0 })
            return null;
        try
        {
            var bytes = ProtectedData.Unprotect(
                host.EncryptedPassword, null, DataProtectionScope.CurrentUser);
            return Encoding.UTF8.GetString(bytes);
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "Failed to decrypt password for host '{Host}'.", host.Name);
            return null;
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private ConnectionOptions BuildConnectionOptions(HyperVHost host)
    {
        var opts = new ConnectionOptions
        {
            EnablePrivileges = true,
            Authentication   = AuthenticationLevel.PacketPrivacy,
            Impersonation    = ImpersonationLevel.Impersonate,
        };

        if (!string.IsNullOrWhiteSpace(host.Username))
        {
            opts.Username = host.Username;
            opts.Password = GetPassword(host) ?? string.Empty;
        }

        return opts;
    }

    private static byte[]? EncryptPassword(string? plaintext)
    {
        if (string.IsNullOrEmpty(plaintext)) return null;
        return ProtectedData.Protect(
            Encoding.UTF8.GetBytes(plaintext), null, DataProtectionScope.CurrentUser);
    }

    public ValueTask DisposeAsync()
    {
        _gate.Dispose();
        return ValueTask.CompletedTask;
    }
}
