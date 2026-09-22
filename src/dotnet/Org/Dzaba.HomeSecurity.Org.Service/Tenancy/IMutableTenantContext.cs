using Dzaba.HomeSecurity.Domain;

namespace Dzaba.HomeSecurity.Org.Service.Tenancy;

/// <summary>
/// The write side of <see cref="ITenantContext"/> for this service - set
/// exactly once per request by <see cref="TenantResolutionMiddleware"/>
/// (or, for org creation, by the handler minting the new tenant id) before
/// any org-scoped <c>AppDbContext</c> is constructed in that request scope.
/// </summary>
public interface IMutableTenantContext : ITenantContext
{
    void SetTenantId(Guid tenantId);
}
