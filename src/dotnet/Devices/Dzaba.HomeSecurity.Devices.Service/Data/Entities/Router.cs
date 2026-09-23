using Dzaba.HomeSecurity.Domain;

namespace Dzaba.HomeSecurity.Devices.Service.Data.Entities;

internal enum RouterProtocol
{
    WebScrape,
    SNMP
}

internal enum RouterAuthMode
{
    HttpBasic,
    HttpDigest,
    FormLogin
}

/// <summary>
/// A home router an org admin provisions from the Admin UI, per
/// docs/architecture/15-router-credentials.md. The secret is only ever
/// stored as ciphertext (ASP.NET Core Data Protection), never plaintext.
/// </summary>
internal sealed class Router : ITenantOwned
{
    public Guid Id { get; set; }

    public Guid TenantId { get; set; }

    public required string Name { get; set; }

    public required string Host { get; set; }

    public RouterProtocol Protocol { get; set; }

    /// <summary>Only meaningful for <see cref="RouterProtocol.WebScrape"/>.</summary>
    public RouterAuthMode? AuthMode { get; set; }

    public required string Username { get; set; }

    public required byte[] EncryptedSecret { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }

    Guid? ITenantOwned.TenantId => TenantId;
}
