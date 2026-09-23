using System.Net;
using System.Runtime.CompilerServices;
using Dzaba.AspNetUtils;
using Dzaba.HomeSecurity.Authorization;
using Dzaba.HomeSecurity.Data;
using Dzaba.HomeSecurity.Domain;
using Dzaba.HomeSecurity.Org.Service.Mapping;
using Microsoft.EntityFrameworkCore;
using UserRoleEntity = Dzaba.HomeSecurity.Data.Entities.UserRole;
using UserRoleAssignment = Dzaba.HomeSecurity.Org.Contracts.UserRoleAssignment;
using AssignUserRole = Dzaba.HomeSecurity.Org.Contracts.AssignUserRole;

namespace Dzaba.HomeSecurity.Org.Service.Services;

internal sealed class UserRoleAssignmentsService : IUserRoleAssignmentsService
{
    private readonly Func<AppDbContext> dbFactory;
    private readonly ITenantContext tenantContext;
    private readonly IPermissionEvaluator permissionEvaluator;
    private readonly ILogger<UserRoleAssignmentsService> logger;

    public UserRoleAssignmentsService(Func<AppDbContext> dbFactory, ITenantContext tenantContext,
        IPermissionEvaluator permissionEvaluator, ILogger<UserRoleAssignmentsService> logger)
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

    public async IAsyncEnumerable<UserRoleAssignment> ListAsync([EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var db = dbFactory();

        await foreach (var entity in db.UserRoles.AsAsyncEnumerable().WithCancellation(cancellationToken))
        {
            yield return entity.ToContract();
        }
    }

    public async Task<UserRoleAssignment> AssignAsync(AssignUserRole request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var db = dbFactory();
        var tenantId = tenantContext.TenantId;
        var entity = new UserRoleEntity { UserId = request.UserId, TenantId = tenantId, RoleId = request.RoleId };
        db.UserRoles.Add(entity);

        try
        {
            await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        // DbUpdateException is what a real relational provider (Npgsql)
        // throws for a PK/unique violation. ArgumentException ("An item
        // with the same key has already been added") is what EF Core's
        // InMemory provider throws instead for the exact same case - not
        // wrapped as a DbUpdateException at all, a real quirk of that
        // provider, not a hypothetical. Both are handled, narrowly, only
        // around this one Add+SaveChanges: the only realistic failure here
        // is the (user, tenant, role) primary key, or roleId not
        // referencing a role visible to this tenant.
        catch (Exception ex) when (ex is DbUpdateException or ArgumentException)
        {
            logger.LogWarning(ex, "AssignAsync SaveChanges failed ({ExceptionType}): {Message}", ex.GetType().FullName, ex.Message);

            throw new HttpResponseException(HttpStatusCode.Conflict,
                $"User '{request.UserId}' already holds role '{request.RoleId}' in this organization, or the role does not exist.", ex);
        }

        await permissionEvaluator.InvalidateAsync(tenantId, request.UserId, cancellationToken).ConfigureAwait(false);

        logger.LogInformation("Role {RoleId} assigned to user {UserId} in organization {OrganizationId}",
            request.RoleId, request.UserId, tenantId);

        return entity.ToContract();
    }

    public async Task<bool> UnassignAsync(string userId, Guid roleId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(userId);

        var db = dbFactory();
        var tenantId = tenantContext.TenantId;

        var entity = await db.UserRoles
            .FirstOrDefaultAsync(ur => ur.UserId == userId && ur.TenantId == tenantId && ur.RoleId == roleId, cancellationToken)
            .ConfigureAwait(false);

        if (entity is null)
        {
            return false;
        }

        db.UserRoles.Remove(entity);
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        await permissionEvaluator.InvalidateAsync(tenantId, userId, cancellationToken).ConfigureAwait(false);

        logger.LogInformation("Role {RoleId} unassigned from user {UserId} in organization {OrganizationId}",
            roleId, userId, tenantId);

        return true;
    }
}
