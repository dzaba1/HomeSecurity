using Dzaba.Org.Contracts;
using Finbuckle.MultiTenant.Abstractions;
using Microsoft.Extensions.Logging;

namespace Dzaba.Org;

public interface IOrganizationServiceInternal
{
    Task<Organization> CreateOrgAsync(string orgName, string userId);
}

internal sealed class OrganizationServiceInternal : IOrganizationServiceInternal
{
    private readonly OrgDbContext dbContext;
    private readonly ILogger<OrgService> logger;

    public OrganizationServiceInternal(OrgDbContext dbContext,
        ILogger<OrgService> logger)
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

        var id = Guid.NewGuid();
        var tenant = new TenantInfo
        {
            Id = id.ToString(),
            Identifier = orgName.ToLowerInvariant().Replace(' ', '-'),
            Name = orgName
        };

        dbContext.TenantInfo.Add(tenant);
        await dbContext.SaveChangesAsync().ConfigureAwait(false);

        return new Organization
        {
            Id = id,
            Identifier = tenant.Identifier,
            Name = orgName
        };

    }
}
