using System.Net;
using System.Runtime.CompilerServices;
using Dzaba.AspNetUtils;
using Dzaba.HomeSecurity.Authorization;
using Dzaba.HomeSecurity.Org.Service.Data;
using Dzaba.HomeSecurity.Domain;
using Dzaba.HomeSecurity.DomainEvents;
using Dzaba.HomeSecurity.Org.Service.Mapping;
using Microsoft.EntityFrameworkCore;
using RoleEntity = Dzaba.HomeSecurity.Org.Service.Data.Entities.Role;
using RolePermissionEntity = Dzaba.HomeSecurity.Org.Service.Data.Entities.RolePermission;
using Role = Dzaba.HomeSecurity.Org.Contracts.Role;
using CreateRole = Dzaba.HomeSecurity.Org.Contracts.CreateRole;

namespace Dzaba.HomeSecurity.Org.Service.Services;

internal sealed class RolesService : IRolesService
{
    private readonly Func<AppDbContext> dbFactory;
    private readonly ITenantContext tenantContext;
    private readonly IPermissionEvaluator permissionEvaluator;
    private readonly IDomainEventPublisher eventPublisher;
    private readonly ILogger<RolesService> logger;

    public RolesService(Func<AppDbContext> dbFactory, ITenantContext tenantContext,
        IPermissionEvaluator permissionEvaluator, IDomainEventPublisher eventPublisher, ILogger<RolesService> logger)
    {
        ArgumentNullException.ThrowIfNull(dbFactory);
        ArgumentNullException.ThrowIfNull(tenantContext);
        ArgumentNullException.ThrowIfNull(permissionEvaluator);
        ArgumentNullException.ThrowIfNull(eventPublisher);
        ArgumentNullException.ThrowIfNull(logger);

        this.dbFactory = dbFactory;
        this.tenantContext = tenantContext;
        this.permissionEvaluator = permissionEvaluator;
        this.eventPublisher = eventPublisher;
        this.logger = logger;
    }

    public async IAsyncEnumerable<Role> ListAsync([EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var db = dbFactory();

        var query = db.Roles.Include(r => r.RolePermissions);
        await foreach (var entity in query.AsAsyncEnumerable().WithCancellation(cancellationToken))
        {
            yield return entity.ToContract();
        }
    }

    public async Task<Role?> GetAsync(Guid roleId, CancellationToken cancellationToken)
    {
        var entity = await FindAsync(dbFactory(), roleId, cancellationToken).ConfigureAwait(false);
        return entity?.ToContract();
    }

    public async Task<Role> CreateAsync(CreateRole request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ValidatePermissionKeys(request.PermissionKeys);

        var db = dbFactory();
        var entity = new RoleEntity { Id = Guid.NewGuid(), TenantId = tenantContext.TenantId, Name = request.Name };
        db.Roles.Add(entity);
        foreach (var permissionKey in request.PermissionKeys.Distinct())
        {
            db.RolePermissions.Add(new RolePermissionEntity { RoleId = entity.Id, PermissionKey = permissionKey });
        }

        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        logger.LogInformation("Role {RoleId} created in organization {OrganizationId}", entity.Id, tenantContext.TenantId);

        await eventPublisher.PublishAsync(DomainEventNames.RoleCreated, DomainEventTargetTypes.Role, entity.Id.ToString(),
            new Dictionary<string, object?>
            {
                ["name"] = request.Name,
                ["permissionKeys"] = request.PermissionKeys.Distinct().Order().ToArray(),
            },
            cancellationToken: cancellationToken).ConfigureAwait(false);

        // No cache invalidation needed - a brand-new role has no UserRole
        // assignments yet, so no cached permission set can reference it.
        entity.RolePermissions = request.PermissionKeys.Distinct()
            .Select(k => new RolePermissionEntity { RoleId = entity.Id, PermissionKey = k })
            .ToList();
        return entity.ToContract();
    }

    public async Task<Role?> UpdateAsync(Guid roleId, CreateRole request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ValidatePermissionKeys(request.PermissionKeys);

        var db = dbFactory();
        var entity = await FindAsync(db, roleId, cancellationToken).ConfigureAwait(false);
        if (entity is null)
        {
            return null;
        }

        ThrowIfSystemRole(entity);

        // Every user currently holding this role - must be gathered before
        // the permission-set change, so cache invalidation covers exactly
        // the affected users (see docs/architecture/07-caching-and-idempotency.md).
        var affectedUserIds = await db.UserRoles
            .Where(ur => ur.RoleId == roleId && ur.TenantId == tenantContext.TenantId)
            .Select(ur => ur.UserId)
            .ToArrayAsync(cancellationToken)
            .ConfigureAwait(false);

        // The before/after of what changed, captured before the old rows go.
        var previousName = entity.Name;
        var previousKeys = entity.RolePermissions.Select(rp => rp.PermissionKey).ToHashSet();

        entity.Name = request.Name;
        db.RolePermissions.RemoveRange(entity.RolePermissions);
        var newPermissions = request.PermissionKeys.Distinct()
            .Select(k => new RolePermissionEntity { RoleId = roleId, PermissionKey = k })
            .ToList();
        db.RolePermissions.AddRange(newPermissions);

        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        foreach (var userId in affectedUserIds)
        {
            await permissionEvaluator.InvalidateAsync(tenantContext.TenantId, userId, cancellationToken).ConfigureAwait(false);
        }

        logger.LogInformation("Role {RoleId} updated in organization {OrganizationId}", roleId, tenantContext.TenantId);

        var newKeys = newPermissions.Select(rp => rp.PermissionKey).ToHashSet();
        await eventPublisher.PublishAsync(DomainEventNames.RoleUpdated, DomainEventTargetTypes.Role, roleId.ToString(),
            new Dictionary<string, object?>
            {
                ["previousName"] = previousName,
                ["name"] = request.Name,
                ["addedPermissionKeys"] = newKeys.Except(previousKeys).Order().ToArray(),
                ["removedPermissionKeys"] = previousKeys.Except(newKeys).Order().ToArray(),
            },
            cancellationToken: cancellationToken).ConfigureAwait(false);

        entity.RolePermissions = newPermissions;
        return entity.ToContract();
    }

    public async Task<bool> DeleteAsync(Guid roleId, CancellationToken cancellationToken)
    {
        var db = dbFactory();
        var entity = await FindAsync(db, roleId, cancellationToken).ConfigureAwait(false);
        if (entity is null)
        {
            return false;
        }

        ThrowIfSystemRole(entity);

        // Loaded (not just projected) and explicitly removed rather than
        // relying on the database's FK cascade: that's real behavior on
        // Postgres, but EF Core only cascades through the change tracker
        // for navigations it has actually loaded, which a relational FK
        // cascade is not - relying on it silently depends on which
        // IDbServerProvider is configured.
        var affectedUserRoles = await db.UserRoles
            .Where(ur => ur.RoleId == roleId && ur.TenantId == tenantContext.TenantId)
            .ToArrayAsync(cancellationToken)
            .ConfigureAwait(false);
        db.UserRoles.RemoveRange(affectedUserRoles);

        db.Roles.Remove(entity);
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        foreach (var userId in affectedUserRoles.Select(ur => ur.UserId).Distinct())
        {
            await permissionEvaluator.InvalidateAsync(tenantContext.TenantId, userId, cancellationToken).ConfigureAwait(false);
        }

        logger.LogInformation("Role {RoleId} deleted from organization {OrganizationId}", roleId, tenantContext.TenantId);

        await eventPublisher.PublishAsync(DomainEventNames.RoleDeleted, DomainEventTargetTypes.Role, roleId.ToString(),
            new Dictionary<string, object?>
            {
                ["name"] = entity.Name,
                ["unassignedAssignments"] = affectedUserRoles.Length,
            },
            cancellationToken: cancellationToken).ConfigureAwait(false);

        return true;
    }

    private static Task<RoleEntity?> FindAsync(AppDbContext db, Guid roleId, CancellationToken cancellationToken) =>
        db.Roles.Include(r => r.RolePermissions).FirstOrDefaultAsync(r => r.Id == roleId, cancellationToken);

    private static void ThrowIfSystemRole(RoleEntity entity)
    {
        if (entity.TenantId is null)
        {
            throw new HttpResponseException(HttpStatusCode.Forbidden, "System-default roles cannot be modified or deleted.");
        }
    }

    private static void ValidatePermissionKeys(IEnumerable<string> permissionKeys)
    {
        var unknown = permissionKeys.Where(k => !PermissionKeys.All.Contains(k)).ToArray();
        if (unknown.Length > 0)
        {
            throw new HttpResponseException(HttpStatusCode.BadRequest,
                $"Unknown permission key(s): {string.Join(", ", unknown)}");
        }
    }
}
