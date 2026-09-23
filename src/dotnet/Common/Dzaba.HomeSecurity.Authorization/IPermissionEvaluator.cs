namespace Dzaba.HomeSecurity.Authorization;

/// <summary>
/// Answers "does this user hold this permission in this tenant" and "is
/// this user a member of this tenant at all", both backed by the same
/// cached AccessContext round-trip described in
/// docs/architecture/07-caching-and-idempotency.md. Independent of any
/// ambient per-request tenant context - every call takes its tenant
/// explicitly, so it stays usable both from an authorization handler
/// (ambient tenant already resolved), from tenant-resolution middleware,
/// and from write-path invalidation logic (acting on a tenant/user pair
/// that isn't the caller's).
/// </summary>
public interface IPermissionEvaluator
{
    Task<bool> HasPermissionAsync(string userId, Guid tenantId, string permissionKey, CancellationToken cancellationToken);

    Task<bool> IsMemberAsync(string userId, Guid tenantId, CancellationToken cancellationToken);

    /// <summary>Invalidates the cached access context for one (tenant, user) pair.</summary>
    Task InvalidateAsync(Guid tenantId, string userId, CancellationToken cancellationToken);
}
