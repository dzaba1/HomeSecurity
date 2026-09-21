namespace Dzaba.HomeSecurity.Data.Entities;

/// <summary>
/// Join table: which permission keys a role grants. Tenant isolation flows
/// transitively through <see cref="Role"/> (no TenantId column of its own),
/// so it does not implement ITenantOwned directly - its query filter is
/// expressed via the Role navigation in AppDbContext.OnModelCreating.
/// </summary>
public sealed class RolePermission
{
    public Guid RoleId { get; set; }

    public Role Role { get; set; } = null!;

    public required string PermissionKey { get; set; }

    public Permission Permission { get; set; } = null!;
}
