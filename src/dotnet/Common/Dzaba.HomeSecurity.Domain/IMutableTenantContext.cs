namespace Dzaba.HomeSecurity.Domain;

/// <summary>
/// The write side of <see cref="ITenantContext"/> - set exactly once per
/// request by tenant-resolution middleware (or, for Org.Service's org
/// creation, by the handler minting the new tenant id) before any
/// org-scoped DbContext is constructed in that request scope. Everything
/// else only ever sees the read side.
/// </summary>
public interface IMutableTenantContext : ITenantContext
{
    void SetTenantId(Guid tenantId);
}
