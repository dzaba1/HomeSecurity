namespace Dzaba.HomeSecurity.Domain;

/// <summary>
/// Resolves the tenant (Organization) the current request is scoped to.
/// Implementations resolve this from the authenticated identity (human JWT
/// tenant claim, or device JWT tenant_id claim) - never from a
/// client-supplied header - per docs/architecture/02-multi-tenancy.md.
/// </summary>
public interface ITenantContext
{
    Guid TenantId { get; }
}
