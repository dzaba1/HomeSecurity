namespace Dzaba.HomeSecurity.Authorization;

/// <summary>
/// Loads a user's access context from the source of truth on a cache
/// miss. Org.Service's implementation queries its own Membership/UserRole/
/// RolePermission tables directly; a service that doesn't own that data
/// (e.g. Devices.Service) implements this by calling Org.Service's
/// access-context API instead - PermissionEvaluator doesn't know or care
/// which.
/// </summary>
public interface IPermissionSourceLoader
{
    Task<AccessContext> LoadAsync(Guid tenantId, string userId, CancellationToken cancellationToken);
}
