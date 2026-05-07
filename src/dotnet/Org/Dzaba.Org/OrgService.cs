using Dzaba.Org.Contracts;
using Finbuckle.MultiTenant.Abstractions;
using Finbuckle.MultiTenant.AspNetCore.Extensions;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Dzaba.Org;

internal sealed class OrgService : IOrgService
{
    private readonly OrgDbContext dbContext;
    private readonly ILogger<OrgService> logger;

    public OrgService(OrgDbContext dbContext,
        ILogger<OrgService> logger)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        ArgumentNullException.ThrowIfNull(logger);

        this.dbContext = dbContext;
        this.logger = logger;
    }

    public async Task<Organization> CreateOrgAsync(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        var id = Guid.NewGuid();
        var tenant = new TenantInfo
        {
            Id = id.ToString(),
            Identifier = name.ToLowerInvariant().Replace(' ', '-'),
            Name = name
        };
        dbContext.TenantInfo.Add(tenant);
        await dbContext.SaveChangesAsync();
        return new Organization
        {
            Id = id,
            Identifier = tenant.Identifier,
            Name = name
        };
    }

    public async Task<string> GetTenantIdAsync(HttpContext context)
    {
        var tenantContext = context.GetMultiTenantContext<TenantInfo>();
        var tenant = tenantContext.TenantInfo;
        return tenant?.Id;
    }

    public async Task<bool> HasAccessAsync(string userId, string tenantId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);

        logger.LogDebug("Checking access for user {UserId} to tenant {TenantId}", userId, tenantId);

        return await dbContext.Memberships.AnyAsync(m => m.UserId == userId && m.TenantId == tenantId)
            .ConfigureAwait(false);
    }
}
