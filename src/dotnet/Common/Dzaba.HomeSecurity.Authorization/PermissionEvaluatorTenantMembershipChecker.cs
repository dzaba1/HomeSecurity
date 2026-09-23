using Dzaba.HomeSecurity.Domain;

namespace Dzaba.HomeSecurity.Authorization;

/// <summary>
/// Answers tenant membership from the same cached access-context round-trip
/// <see cref="IPermissionEvaluator"/> uses for permission checks - for a
/// service that doesn't own Membership directly (e.g. Devices.Service),
/// where the cache-miss path reaches Org.Service instead of a local table.
/// Registered via Bootstrapper.AddDzabaHomeSecurityCachedTenantMembership.
/// </summary>
internal sealed class PermissionEvaluatorTenantMembershipChecker : ITenantMembershipChecker
{
    private readonly IPermissionEvaluator permissionEvaluator;

    public PermissionEvaluatorTenantMembershipChecker(IPermissionEvaluator permissionEvaluator)
    {
        ArgumentNullException.ThrowIfNull(permissionEvaluator);

        this.permissionEvaluator = permissionEvaluator;
    }

    public Task<bool> IsMemberAsync(Guid tenantId, string userId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(userId);

        return permissionEvaluator.IsMemberAsync(userId, tenantId, cancellationToken);
    }
}
