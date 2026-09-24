using Dzaba.HomeSecurity.Domain;

namespace Dzaba.HomeSecurity.Org.Service.Data.Entities;

public enum DeviceCredentialStatus
{
    Active,
    Revoked
}

/// <summary>
/// An agent-app credential (device identity), owned by DeviceAuth. Not to be
/// confused with a network device/MAC address seen in router logs, which is
/// Devices.Service's own Device entity, in its own database - see
/// docs/decisions/0016-devices-service-owns-its-own-database.md.
/// </summary>
public sealed class DeviceCredential : ITenantOwned
{
    public Guid Id { get; set; }

    public Guid TenantId { get; set; }

    public required string Name { get; set; }

    public required string SecretHash { get; set; }

    public DateTimeOffset SecretCreatedAt { get; set; }

    public DeviceCredentialStatus Status { get; set; }

    public DateTimeOffset? RevokedAt { get; set; }

    Guid? ITenantOwned.TenantId => TenantId;
}
