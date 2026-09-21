using Dzaba.AspNetUtils;
using Dzaba.Org.Contracts;
using Finbuckle.MultiTenant.AspNetCore.Extensions;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Dzaba.Org;

public interface IOrganizationServiceInternal
{
    Task<Organization> CreateOrgAsync(string orgName, string userId);
    Task<bool> HasAccessAsync(string userId, GuidTenantInfo tenant);
    GuidTenantInfo GetTenant(HttpContext context);
    Task<GuidTenantInfo> TryGetTenantWithAccessAsync(HttpContext context);
}

internal sealed class OrganizationServiceInternal : IOrganizationServiceInternal
{
    private readonly OrgDbContext dbContext;
    private readonly ILogger<OrganizationServiceInternal> logger;

    public OrganizationServiceInternal(OrgDbContext dbContext,
        ILogger<OrganizationServiceInternal> logger)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        ArgumentNullException.ThrowIfNull(logger);

        this.dbContext = dbContext;
        this.logger = logger;
    }

    public async Task<Organization> CreateOrgAsync(string orgName, string userId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(orgName);
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);

        logger.LogInformation("User {UserId} is creating a new organization named {OrganizationName}", userId, orgName);

        var tenant = new GuidTenantInfo
        {
            GuidId = Guid.NewGuid(),
            Identifier = orgName.ToLowerInvariant().Replace(' ', '-'),
            Name = orgName
        };

        dbContext.Tenants.Add(tenant);

        var membership = new Membership
        {
            UserId = userId,
            TenantId = tenant.GuidId
        };
        dbContext.Memberships.Add(membership);

        var adminRole = new Role
        {
            IsInternal = true,
            Id = Guid.NewGuid(),
            Name = "Admin",
            OrganizationId = tenant.GuidId
        };
        dbContext.Roles.Add(adminRole);

        var roleMembership = new RoleMembership
        {
            RoleId = adminRole.Id,
            UserId = userId
        };
        dbContext.RoleMemberships.Add(roleMembership);

        await dbContext.SaveChangesAsync().ConfigureAwait(false);

        return new Organization
        {
            Identifier = tenant.Identifier,
            Name = orgName
        };
    }

    public GuidTenantInfo GetTenant(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        logger.LogDebug("Checking tenant identifier from HTTP header.");

        var tenantContext = context.GetMultiTenantContext<GuidTenantInfo>();
        return tenantContext.TenantInfo;
    }

    public async Task<bool> HasAccessAsync(string userId, GuidTenantInfo tenant)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);
        ArgumentNullException.ThrowIfNull(tenant);

        logger.LogDebug("Checking access for user {UserId} to tenant {TenantIdentifier}", userId, tenant.Identifier);

        return await dbContext.Memberships.AnyAsync(m => m.UserId == userId && m.TenantId == tenant.GuidId)
            .ConfigureAwait(false);
    }

    public async Task<GuidTenantInfo> TryGetTenantWithAccessAsync(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var userId = context.GetUserSubOrNameIdentifier();
        if (string.IsNullOrEmpty(userId))
        {
            return null;
        }

        var tenant = GetTenant(context);
        if (tenant == null)
        {
            return null;
        }

        var hasAccess = await HasAccessAsync(userId, tenant).ConfigureAwait(false);
        if (!hasAccess)
        {
            return null;
        }

        return tenant;
    }
}
