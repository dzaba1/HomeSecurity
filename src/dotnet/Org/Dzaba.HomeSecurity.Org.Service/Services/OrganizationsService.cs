using System.Net;
using System.Runtime.CompilerServices;
using Dzaba.AspNetUtils;
using Dzaba.HomeSecurity.Data;
using Dzaba.HomeSecurity.Domain;
using Dzaba.HomeSecurity.Org.Service.Mapping;
using Dzaba.HomeSecurity.Org.Service.Tenancy;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using OrganizationEntity = Dzaba.HomeSecurity.Data.Entities.Organization;
using UserRoleEntity = Dzaba.HomeSecurity.Data.Entities.UserRole;
using MembershipEntity = Dzaba.HomeSecurity.Data.Entities.Membership;
using Organization = Dzaba.HomeSecurity.Org.Contracts.Organization;
using CreateOrganization = Dzaba.HomeSecurity.Org.Contracts.CreateOrganization;

namespace Dzaba.HomeSecurity.Org.Service.Services;

internal sealed class OrganizationsService : IOrganizationsService
{
    private readonly Func<AppDbContext> dbFactory;
    private readonly IMutableTenantContext tenantContext;
    private readonly ILogger<OrganizationsService> logger;

    public OrganizationsService(Func<AppDbContext> dbFactory, IMutableTenantContext tenantContext, ILogger<OrganizationsService> logger)
    {
        ArgumentNullException.ThrowIfNull(dbFactory);
        ArgumentNullException.ThrowIfNull(tenantContext);
        ArgumentNullException.ThrowIfNull(logger);

        this.dbFactory = dbFactory;
        this.tenantContext = tenantContext;
        this.logger = logger;
    }

    public async Task<Organization> CreateAsync(CreateOrganization request, string creatorUserId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrEmpty(creatorUserId);

        var orgId = Guid.NewGuid();

        // Must be set before the first dbFactory() call: AppDbContext binds
        // its query filters to the tenant at construction time, and this
        // request's tenant (the org being created) doesn't exist until now.
        tenantContext.SetTenantId(orgId);

        var db = dbFactory();

        var entity = new OrganizationEntity { Id = orgId, Identifier = request.Identifier, Name = request.Name };
        db.Organizations.Add(entity);
        db.Memberships.Add(new MembershipEntity { OrganizationId = orgId, UserId = creatorUserId });
        // Reuse the existing seeded system Owner role rather than minting a
        // new per-org role - see the plan's "Org creation flow" section.
        db.UserRoles.Add(new UserRoleEntity { UserId = creatorUserId, TenantId = orgId, RoleId = SystemRoles.OwnerId });

        try
        {
            await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (DbUpdateException ex) when (IsUniqueViolation(ex))
        {
            throw new HttpResponseException(HttpStatusCode.Conflict,
                $"An organization with identifier '{request.Identifier}' already exists.", ex);
        }

        logger.LogInformation("Organization {OrganizationId} created by {UserId}", orgId, creatorUserId);

        return entity.ToContract();
    }

    public async Task<Organization?> GetAsync(Guid orgId, CancellationToken cancellationToken)
    {
        var db = dbFactory();
        var entity = await db.Organizations.FindAsync([orgId], cancellationToken).ConfigureAwait(false);
        return entity?.ToContract();
    }

    public async IAsyncEnumerable<Organization> ListForUserAsync(string userId, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(userId);

        var db = dbFactory();

        // The one deliberate cross-tenant query in this service. The
        // explicit UserId predicate - not IgnoreQueryFilters() alone - is
        // what keeps this scoped to the caller's own orgs; see the plan's
        // "Cross-tenant exception" note.
        var orgIds = db.Memberships.IgnoreQueryFilters()
            .Where(m => m.UserId == userId)
            .Select(m => m.OrganizationId);

        var query = db.Organizations.Where(o => orgIds.Contains(o.Id));

        await foreach (var entity in query.AsAsyncEnumerable().WithCancellation(cancellationToken))
        {
            yield return entity.ToContract();
        }
    }

    private static bool IsUniqueViolation(DbUpdateException ex) =>
        ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation };
}
