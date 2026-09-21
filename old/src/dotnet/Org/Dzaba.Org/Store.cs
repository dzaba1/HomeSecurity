using Finbuckle.MultiTenant.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace Dzaba.Org;

internal sealed class Store : IMultiTenantStore<GuidTenantInfo>
{
    private readonly OrgDbContext dbContext;

    public Store(OrgDbContext dbContext)
    {
        ArgumentNullException.ThrowIfNull(dbContext);

        this.dbContext = dbContext;
    }

    public async Task<bool> AddAsync(GuidTenantInfo tenantInfo)
    {
        ArgumentNullException.ThrowIfNull(tenantInfo);

        await dbContext.Tenants.AddAsync(tenantInfo).ConfigureAwait(false);
        var result = await dbContext.SaveChangesAsync().ConfigureAwait(false) > 0;
        dbContext.Entry(tenantInfo).State = EntityState.Detached;

        return result;
    }

    public async Task<IEnumerable<GuidTenantInfo>> GetAllAsync()
    {
        return await dbContext.Tenants.AsNoTracking().ToListAsync().ConfigureAwait(false);
    }

    public async Task<IEnumerable<GuidTenantInfo>> GetAllAsync(int take, int skip)
    {
        return await dbContext.Tenants.Skip(skip).Take(take).AsNoTracking().ToListAsync().ConfigureAwait(false);
    }

    public async Task<GuidTenantInfo> GetAsync(string id)
    {
        return await dbContext.Tenants.AsNoTracking()
            .Where(ti => ti.Id == id)
            .SingleOrDefaultAsync().ConfigureAwait(false);
    }

    public async Task<GuidTenantInfo> GetByIdentifierAsync(string identifier)
    {
        return await dbContext.Tenants.AsNoTracking()
            .Where(ti => ti.Identifier == identifier)
            .SingleOrDefaultAsync().ConfigureAwait(false);
    }

    public async Task<bool> RemoveAsync(string identifier)
    {
        var existing = await dbContext.Tenants
            .Where(ti => ti.Identifier == identifier)
            .SingleOrDefaultAsync().ConfigureAwait(false);

        if (existing is null)
        {
            return false;
        }

        dbContext.Tenants.Remove(existing);
        return await dbContext.SaveChangesAsync().ConfigureAwait(false) > 0;
    }

    public async Task<bool> UpdateAsync(GuidTenantInfo tenantInfo)
    {
        ArgumentNullException.ThrowIfNull(tenantInfo);

        dbContext.Tenants.Update(tenantInfo);
        var result = await dbContext.SaveChangesAsync().ConfigureAwait(false) > 0;
        dbContext.Entry(tenantInfo).State = EntityState.Detached;
        return result;
    }
}