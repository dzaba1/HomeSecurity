namespace Dzaba.HomeSecurity.Data.Entities;

/// <summary>
/// The tenant root. Not itself tenant-owned/query-filtered - see
/// docs/architecture/02-multi-tenancy.md.
/// </summary>
public sealed class Organization
{
    public Guid Id { get; set; }

    public required string Identifier { get; set; }

    public required string Name { get; set; }
}
