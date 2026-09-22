namespace Dzaba.HomeSecurity.Org.Service.Services;

/// <summary>
/// Answers "does this user hold this permission in this tenant", backed by
/// the Redis cache described in docs/architecture/07-caching-and-idempotency.md.
/// Independent of the ambient per-request tenant context - every call takes
/// its tenant explicitly, so it stays usable both from the authorization
/// handler (ambient tenant already resolved) and from write-path
/// invalidation logic (acting on a tenant/user pair that isn't the caller's).
/// </summary>
public interface IPermissionEvaluator
{
    Task<bool> HasPermissionAsync(string userId, Guid tenantId, string permissionKey, CancellationToken cancellationToken);

    /// <summary>Invalidates the cached effective permission set for one (tenant, user) pair.</summary>
    Task InvalidateAsync(Guid tenantId, string userId, CancellationToken cancellationToken);
}
