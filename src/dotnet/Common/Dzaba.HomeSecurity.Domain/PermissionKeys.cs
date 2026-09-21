namespace Dzaba.HomeSecurity.Domain;

/// <summary>
/// The fixed catalog of atomic permission keys - see
/// docs/architecture/04-roles-and-permissions.md. Seeded once via an EF Core
/// migration (AppDbContext.OnModelCreating's Permission HasData); referenced
/// from here by both the seed data and later permission-check call sites so
/// the literal strings are never duplicated.
/// </summary>
public static class PermissionKeys
{
    public const string DeviceView = "device.view";
    public const string DeviceDelete = "device.delete";
    public const string LogsView = "logs.view";
    public const string OrgManageMembers = "org.manage_members";

    public static readonly IReadOnlyList<string> All =
    [
        DeviceView,
        DeviceDelete,
        LogsView,
        OrgManageMembers
    ];
}
