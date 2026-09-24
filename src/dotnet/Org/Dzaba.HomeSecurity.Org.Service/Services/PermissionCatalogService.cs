using System.Runtime.CompilerServices;
using Dzaba.HomeSecurity.Org.Service.Data;
using Dzaba.HomeSecurity.Org.Service.Mapping;
using Microsoft.EntityFrameworkCore;
using Permission = Dzaba.HomeSecurity.Org.Contracts.Permission;

namespace Dzaba.HomeSecurity.Org.Service.Services;

internal sealed class PermissionCatalogService : IPermissionCatalogService
{
    private readonly Func<AppDbContext> dbFactory;

    public PermissionCatalogService(Func<AppDbContext> dbFactory)
    {
        ArgumentNullException.ThrowIfNull(dbFactory);

        this.dbFactory = dbFactory;
    }

    public async IAsyncEnumerable<Permission> ListAsync([EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var db = dbFactory();

        await foreach (var entity in db.Permissions.AsAsyncEnumerable().WithCancellation(cancellationToken))
        {
            yield return entity.ToContract();
        }
    }
}
