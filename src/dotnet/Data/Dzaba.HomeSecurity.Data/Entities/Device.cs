using Dzaba.HomeSecurity.Domain;

namespace Dzaba.HomeSecurity.Data.Entities;

public enum DeviceStatus
{
    Active,
    Revoked
}

/// <summary>
/// An agent-app credential (device identity), owned by DeviceAuth. Not to be
/// confused with a network device/MAC address seen in router logs, which is
/// a separate, future Ingestion-domain concept - see the naming note in
/// docs/decisions/0006-defer-auth-platform-extraction.md's implementation
/// plan.
/// </summary>
public sealed class Device : ITenantOwned
{
    public Guid Id { get; set; }

    public Guid TenantId { get; set; }

    public required string Name { get; set; }

    public required string SecretHash { get; set; }

    public DateTimeOffset SecretCreatedAt { get; set; }

    public DeviceStatus Status { get; set; }

    public DateTimeOffset? RevokedAt { get; set; }

    Guid? ITenantOwned.TenantId => TenantId;
}
