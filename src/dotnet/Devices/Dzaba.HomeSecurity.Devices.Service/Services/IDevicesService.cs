using Dzaba.HomeSecurity.Devices.Contracts;

namespace Dzaba.HomeSecurity.Devices.Service.Services;

/// <summary>
/// Operates within the already-resolved ambient tenant (see
/// <c>TenantResolutionMiddleware</c>). Deliberately has no create or delete:
/// device rows are created and their last-seen fields updated only by the
/// ingestion consumer (docs/architecture/06-notifications.md), and there is no
/// departure modelling - so this API only lists, gets, and renames/acknowledges.
/// </summary>
public interface IDevicesService
{
    IAsyncEnumerable<Device> ListAsync(CancellationToken cancellationToken = default);

    Task<Device?> GetAsync(Guid deviceId, CancellationToken cancellationToken);

    /// <returns>The updated device, or null if it doesn't exist in this tenant.</returns>
    Task<Device?> UpdateAsync(Guid deviceId, UpdateDevice request, CancellationToken cancellationToken);
}
