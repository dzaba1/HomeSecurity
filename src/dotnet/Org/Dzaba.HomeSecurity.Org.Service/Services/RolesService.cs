using System.Net;
using System.Runtime.CompilerServices;
using Dzaba.AspNetUtils;
using Dzaba.HomeSecurity.Data;
using Dzaba.HomeSecurity.Domain;
using Dzaba.HomeSecurity.Org.Service.Mapping;
using Microsoft.EntityFrameworkCore;
using RoleEntity = Dzaba.HomeSecurity.Data.Entities.Role;
using RolePermissionEntity = Dzaba.HomeSecurity.Data.Entities.RolePermission;
using Role = Dzaba.HomeSecurity.Org.Contracts.Role;
using CreateRole = Dzaba.HomeSecurity.Org.Contracts.CreateRole;

namespace Dzaba.HomeSecurity.Org.Service.Services;

internal sealed class RolesService : IRolesService
{
    private readonly Func<AppDbContext> dbFactory;
    private readonly ITenantContext tenantContext;
    private readonly IPermissionEvaluator permissionEvaluator;
    private readonly ILogger<RolesService> logger;

    public RolesService(Func<AppDbContext> dbFactory, ITenantContext tenantContext,
        IPermissionEvaluator permissionEvaluator, ILogger<RolesService> logger)
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

        // Must be queried before the delete: EF's FK cascade removes the
        // RolePermission/UserRole rows automatically, which would otherwise
        // make the affected-user set unrecoverable afterwards.
        var affectedUserIds = await db.UserRoles
            .Where(ur => ur.RoleId == roleId && ur.TenantId == tenantContext.TenantId)
            .Select(ur => ur.UserId)
            .ToArrayAsync(cancellationToken)
            .ConfigureAwait(false);

        db.Roles.Remove(entity);
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        foreach (var userId in affectedUserIds)
        {
            await permissionEvaluator.InvalidateAsync(tenantContext.TenantId, userId, cancellationToken).ConfigureAwait(false);
        }

        logger.LogInformation("Role {RoleId} deleted from organization {OrganizationId}", roleId, tenantContext.TenantId);

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
