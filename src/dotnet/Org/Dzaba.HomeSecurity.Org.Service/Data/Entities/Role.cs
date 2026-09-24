using Dzaba.HomeSecurity.Domain;

namespace Dzaba.HomeSecurity.Org.Service.Data.Entities;

/// <summary>
/// A named, tenant-owned bundle of permissions. A null <see cref="TenantId"/>
/// means a system-default role (Owner/Admin/Member/Viewer), visible to every
/// tenant - see docs/architecture/04-roles-and-permissions.md.
/// </summary>
public sealed class Role : ITenantOwned
{
    public Guid Id { get; set; }

    public Guid? TenantId { get; set; }

    public required string Name { get; set; }

    public ICollection<RolePermission> RolePermissions { get; set; } = [];
}
