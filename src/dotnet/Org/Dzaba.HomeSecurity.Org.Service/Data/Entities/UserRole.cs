using Dzaba.HomeSecurity.Domain;

namespace Dzaba.HomeSecurity.Org.Service.Data.Entities;

/// <summary>
/// Assigns a role to a user, scoped to a tenant - the join the effective
/// permission lookup queries. See docs/architecture/04-roles-and-permissions.md.
/// </summary>
public sealed class UserRole : ITenantOwned
{
    /// <summary>The Keycloak subject ("sub" claim).</summary>
    public required string UserId { get; set; }

    public Guid TenantId { get; set; }

    public Guid RoleId { get; set; }

    public Role Role { get; set; } = null!;

    Guid? ITenantOwned.TenantId => TenantId;
}
