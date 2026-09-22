using Dzaba.HomeSecurity.Org.Contracts;

namespace Dzaba.HomeSecurity.Org.Service.Services;

public interface IOrganizationsService
{
    /// <summary>
    /// Creates the organization and, in the same save, makes the creator a
    /// member holding the system-default Owner role - see the plan's "Org
    /// creation flow" section.
    /// </summary>
    Task<Organization> CreateAsync(CreateOrganization request, string creatorUserId, CancellationToken cancellationToken);

    /// <summary>Looks up an org within the already-resolved ambient tenant.</summary>
    Task<Organization?> GetAsync(Guid orgId, CancellationToken cancellationToken);

    /// <summary>The one deliberate cross-tenant query - every org the user is a member of.</summary>
    IAsyncEnumerable<Organization> ListForUserAsync(string userId, CancellationToken cancellationToken = default);
}
