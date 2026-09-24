using Dzaba.HomeSecurity.Devices.Contracts;

namespace Dzaba.HomeSecurity.Devices.Service.Services;

/// <summary>Operates within the already-resolved ambient tenant (see <c>TenantResolutionMiddleware</c>).</summary>
public interface IRoutersService
{
    IAsyncEnumerable<Router> ListAsync(CancellationToken cancellationToken = default);

    Task<Router?> GetAsync(Guid routerId, CancellationToken cancellationToken);

    Task<Router> CreateAsync(CreateRouter request, CancellationToken cancellationToken);

    /// <returns>The updated router, or null if it doesn't exist in this tenant.</returns>
    Task<Router?> UpdateAsync(Guid routerId, UpdateRouter request, CancellationToken cancellationToken);

    /// <returns>False if the router doesn't exist in this tenant.</returns>
    Task<bool> DeleteAsync(Guid routerId, CancellationToken cancellationToken);
}
