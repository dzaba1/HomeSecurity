namespace Dzaba.HomeSecurity.Domain;

/// <summary>
/// The fixed catalog of atomic permission keys - see
/// docs/architecture/04-roles-and-permissions.md. Seeded once via an EF Core
/// migration (AppDbContext.OnModelCreating's Permission HasData); referenced
/// from here by both the seed data and later permission-check call sites so
/// the literal strings are never duplicated.
///
/// device_credential.* and device.* are deliberately two distinct pairs, not
/// one reused device.* pair for both meanings: device_credential.* gates the
/// agent-auth DeviceCredential entity (ADR-0004), device.* gates the
/// network-device entity Devices.Service owns (docs/architecture/06-notifications.md).
/// A role already granted view access to one must not silently gain it for
/// the other - see ADR-0016's consequences.
/// </summary>
public static class PermissionKeys
{
    public const string DeviceCredentialView = "device_credential.view";
    public const string DeviceCredentialDelete = "device_credential.delete";
    public const string LogsView = "logs.view";
    public const string OrgManageMembers = "org.manage_members";
    public const string RouterView = "router.view";
    public const string RouterManage = "router.manage";
    public const string DeviceView = "device.view";
    public const string DeviceManage = "device.manage";

    public static readonly IReadOnlyList<string> All =
    [
        DeviceCredentialView,
        DeviceCredentialDelete,
        LogsView,
        OrgManageMembers,
        RouterView,
        RouterManage,
        DeviceView,
        DeviceManage
    ];
}
