using Dzaba.HomeSecurity.Org.Contracts;

namespace Dzaba.HomeSecurity.Org.Service.Services;

/// <summary>The global permission catalog - not tenant-scoped, no <c>{orgId}</c> route involved.</summary>
public interface IPermissionCatalogService
{
    IAsyncEnumerable<Permission> ListAsync(CancellationToken cancellationToken = default);
}
