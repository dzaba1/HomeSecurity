using Dzaba.HomeSecurity.Domain;

namespace Dzaba.HomeSecurity.Org.Service.Data.Entities;

/// <summary>
/// Which organizations a Keycloak-authenticated user belongs to. Distinct
/// from <see cref="UserRole"/>: this is "am I in this org at all", not
/// "what can I do in this org" - see docs/architecture/02-multi-tenancy.md.
/// </summary>
public sealed class Membership : ITenantOwned
{
    public Guid OrganizationId { get; set; }

    /// <summary>The Keycloak subject ("sub" claim).</summary>
    public required string UserId { get; set; }

    Guid? ITenantOwned.TenantId => OrganizationId;
}
