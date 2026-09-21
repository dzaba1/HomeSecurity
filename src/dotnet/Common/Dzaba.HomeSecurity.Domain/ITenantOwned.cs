namespace Dzaba.HomeSecurity.Domain;

/// <summary>
/// Marks an entity as tenant-owned data, so infrastructure can apply one EF
/// Core global query filter to every such entity generically instead of
/// per-entity. TenantId is nullable because some entities (e.g. a
/// system-default Role) are deliberately shared across every tenant; the
/// filter for those is "TenantId is null OR TenantId == current tenant" -
/// entities that always have a tenant simply never produce a null value.
/// </summary>
public interface ITenantOwned
{
    Guid? TenantId { get; }
}
