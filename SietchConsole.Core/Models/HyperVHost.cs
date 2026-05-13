namespace SietchConsole.Core.Models;

/// <summary>
/// A registered Hyper-V host that Sietch Console can manage.
/// The local machine is always present with <see cref="IsLocal"/> = true and cannot be removed.
/// Remote hosts carry DPAPI-encrypted credentials stored in SQLite (#153).
/// </summary>
public class HyperVHost
{
    public string   Id                { get; set; } = Guid.NewGuid().ToString();
    public string   Name              { get; set; } = string.Empty;
    public string   Hostname          { get; set; } = "localhost";
    public int      Port              { get; set; } = 5985;
    public string?  Username          { get; set; }

    /// <summary>DPAPI-encrypted UTF-8 password bytes. Null for local or password-less hosts.</summary>
    public byte[]?  EncryptedPassword { get; set; }

    /// <summary>True for the built-in "This machine" entry. Cannot be deleted or edited.</summary>
    public bool     IsLocal           { get; set; } = false;

    public DateTime CreatedAt         { get; set; } = DateTime.UtcNow;
}
