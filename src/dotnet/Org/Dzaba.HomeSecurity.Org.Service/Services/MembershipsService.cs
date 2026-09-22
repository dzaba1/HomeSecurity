using System.Net;
using System.Runtime.CompilerServices;
using Dzaba.AspNetUtils;
using Dzaba.HomeSecurity.Data;
using Dzaba.HomeSecurity.Domain;
using Dzaba.HomeSecurity.Org.Service.Mapping;
using Microsoft.EntityFrameworkCore;
using MembershipEntity = Dzaba.HomeSecurity.Data.Entities.Membership;
using Membership = Dzaba.HomeSecurity.Org.Contracts.Membership;
using CreateMembership = Dzaba.HomeSecurity.Org.Contracts.CreateMembership;

namespace Dzaba.HomeSecurity.Org.Service.Services;

internal sealed class MembershipsService : IMembershipsService
{
    private readonly Func<AppDbContext> dbFactory;
    private readonly ITenantContext tenantContext;
    private readonly IPermissionEvaluator permissionEvaluator;
    private readonly ILogger<MembershipsService> logger;

    public MembershipsService(Func<AppDbContext> dbFactory, ITenantContext tenantContext,
        IPermissionEvaluator permissionEvaluator, ILogger<MembershipsService> logger)
    {
        ArgumentNullException.ThrowIfNull(dbFactory);
        ArgumentNullException.ThrowIfNull(tenantContext);
        ArgumentNullException.ThrowIfNull(permissionEvaluator);
        ArgumentNullException.ThrowIfNull(logger);

        this.dbFactory = dbFactory;
        this.tenantContext = tenantContext;
        this.permissionEvaluator = permissionEvaluator;
        this.logger = logger;
    }

    public async IAsyncEnumerable<Membership> ListAsync([EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var db = dbFactory();

        await foreach (var entity in db.Memberships.AsAsyncEnumerable().WithCancellation(cancellationToken))
        {
            yield return entity.ToContract();
        }
    }

    public async Task<Membership> AddAsync(CreateMembership request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var db = dbFactory();
        var entity = new MembershipEntity { OrganizationId = tenantContext.TenantId, UserId = request.UserId };
        db.Memberships.Add(entity);

        try
        {
            await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        // DbUpdateException is what a real relational provider (Npgsql)
        // throws for a PK violation; ArgumentException ("An item with the
        // same key has already been added") is what EF Core's InMemory
        // provider throws instead for the exact same case - a real quirk of
        // that provider, not wrapped as DbUpdateException at all. Both are
        // handled, narrowly, only around this one Add+SaveChanges: the only
        // realistic failure here is the (OrganizationId, UserId) primary key.
        catch (Exception ex) when (ex is DbUpdateException or ArgumentException)
        {
            logger.LogWarning(ex, "AddAsync SaveChanges failed ({ExceptionType}): {Message}", ex.GetType().FullName, ex.Message);

            throw new HttpResponseException(HttpStatusCode.Conflict,
                $"User '{request.UserId}' is already a member of this organization.", ex);
        }

        logger.LogInformation("User {UserId} added to organization {OrganizationId}", request.UserId, tenantContext.TenantId);

        return entity.ToContract();
    }

    public async Task<bool> RemoveAsync(string userId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(userId);

        var db = dbFactory();
        var tenantId = tenantContext.TenantId;

        var membership = await db.Memberships
            .FirstOrDefaultAsync(m => m.OrganizationId == tenantId && m.UserId == userId, cancellationToken)
            .ConfigureAwait(false);

        if (membership is null)
        {
            return false;
        }

        db.Memberships.Remove(membership);

        var roleAssignments = await db.UserRoles
            .Where(ur => ur.TenantId == tenantId && ur.UserId == userId)
            .ToArrayAsync(cancellationToken)
            .ConfigureAwait(false);
        db.UserRoles.RemoveRange(roleAssignments);

        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        await permissionEvaluator.InvalidateAsync(tenantId, userId, cancellationToken).ConfigureAwait(false);

        logger.LogInformation("User {UserId} removed from organization {OrganizationId}", userId, tenantId);

        return true;
    }
}
