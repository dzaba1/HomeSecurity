namespace Dzaba.HomeSecurity.Domain;

/// <summary>
/// An <see cref="ITenantContext"/> for a known, fixed tenant - for design-time
/// tooling (EF migrations) and for callers that need to query a specific
/// tenant explicitly rather than the ambient request's tenant (e.g. the
/// permission cache's cache-miss lookup for an arbitrary (tenantId, userId)).
/// </summary>
public sealed class StaticTenantContext(Guid tenantId) : ITenantContext
{
    public Guid TenantId { get; } = tenantId;
}
