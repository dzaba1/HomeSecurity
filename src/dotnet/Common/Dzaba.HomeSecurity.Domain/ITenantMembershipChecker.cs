namespace Dzaba.HomeSecurity.Domain;

/// <summary>
/// Answers "is this user a member of this tenant" for tenant-resolution
/// middleware. A service that owns Membership directly (Org.Service)
/// queries its own table; a service that doesn't (e.g. Devices.Service)
/// answers from the shared access-context cache instead - see
/// docs/decisions/0016-devices-service-owns-its-own-database.md.
/// </summary>
public interface ITenantMembershipChecker
{
    Task<bool> IsMemberAsync(Guid tenantId, string userId, CancellationToken cancellationToken);
}
