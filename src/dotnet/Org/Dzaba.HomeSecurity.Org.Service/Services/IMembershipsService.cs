using Dzaba.HomeSecurity.Org.Contracts;

namespace Dzaba.HomeSecurity.Org.Service.Services;

/// <summary>Operates within the already-resolved ambient tenant (see <c>TenantResolutionMiddleware</c>).</summary>
public interface IMembershipsService
{
    IAsyncEnumerable<Membership> ListAsync(CancellationToken cancellationToken = default);

    Task<Membership> AddAsync(CreateMembership request, CancellationToken cancellationToken);

    /// <summary>Removes the membership and any role assignments the user held in this tenant.</summary>
    Task<bool> RemoveAsync(string userId, CancellationToken cancellationToken);
}
