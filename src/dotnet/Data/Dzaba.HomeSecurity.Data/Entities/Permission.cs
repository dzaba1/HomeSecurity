namespace Dzaba.HomeSecurity.Data.Entities;

/// <summary>
/// One atomic, opaque permission key from the fixed catalog. Seeded at
/// deploy time, not tenant-scoped - see
/// docs/architecture/04-roles-and-permissions.md.
/// </summary>
public sealed class Permission
{
    public required string Key { get; set; }

    public required string Description { get; set; }
}
