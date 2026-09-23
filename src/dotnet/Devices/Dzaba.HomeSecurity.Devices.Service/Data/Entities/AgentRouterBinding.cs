using Dzaba.HomeSecurity.Domain;

namespace Dzaba.HomeSecurity.Devices.Service.Data.Entities;

/// <summary>
/// "This agent credential polls this router." Lives here rather than as a
/// column on Org.Service's DeviceCredential because a cross-database
/// foreign key isn't possible (ADR-0016) - DeviceCredentialId is an opaque
/// reference (the device token's device_id claim), not a real FK.
/// </summary>
internal sealed class AgentRouterBinding : ITenantOwned
{
    public Guid Id { get; set; }

    public Guid TenantId { get; set; }

    public Guid DeviceCredentialId { get; set; }

    public Guid RouterId { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    Guid? ITenantOwned.TenantId => TenantId;
}
