using Dzaba.HomeSecurity.Domain;

namespace Dzaba.HomeSecurity.Devices.Service.Data.Entities;

internal enum DeviceStatus
{
    Unknown,
    Known
}

/// <summary>
/// A network device (MAC address) seen on a tenant's home network, per
/// docs/architecture/06-notifications.md - not to be confused with the
/// agent-auth DeviceCredential, which lives in Org.Service's database.
/// Identity is (TenantId, MacAddress); LastSeenViaRouterId is traceability
/// only and no admin-facing endpoint writes it.
/// </summary>
internal sealed class Device : ITenantOwned
{
    public Guid Id { get; set; }

    public Guid TenantId { get; set; }

    public required string MacAddress { get; set; }

    public string? Name { get; set; }

    public DeviceStatus Status { get; set; }

    public DateTimeOffset FirstSeenAt { get; set; }

    public DateTimeOffset LastSeenAt { get; set; }

    public Guid? LastSeenViaRouterId { get; set; }

    Guid? ITenantOwned.TenantId => TenantId;
}
