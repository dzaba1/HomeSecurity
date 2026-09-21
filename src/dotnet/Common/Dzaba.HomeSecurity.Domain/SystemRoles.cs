namespace Dzaba.HomeSecurity.Domain;

/// <summary>
/// The four system-default roles that ship out of the box (Role.TenantId ==
/// null, visible to every tenant) - see
/// docs/architecture/04-roles-and-permissions.md. Fixed Ids so the seed
/// migration's Role.Id / RolePermission.RoleId values are deterministic and
/// stable across environments and future migrations.
/// </summary>
public static class SystemRoles
{
    public static readonly Guid OwnerId = new("00000000-0000-0000-0000-000000000001");
    public static readonly Guid AdminId = new("00000000-0000-0000-0000-000000000002");
    public static readonly Guid MemberId = new("00000000-0000-0000-0000-000000000003");
    public static readonly Guid ViewerId = new("00000000-0000-0000-0000-000000000004");

    public const string OwnerName = "Owner";
    public const string AdminName = "Admin";
    public const string MemberName = "Member";
    public const string ViewerName = "Viewer";
}
