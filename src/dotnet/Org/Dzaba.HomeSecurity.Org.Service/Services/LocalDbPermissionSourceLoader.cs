using Dzaba.HomeSecurity.Authorization;
using Dzaba.HomeSecurity.Data;
using Dzaba.HomeSecurity.Domain;
using Microsoft.EntityFrameworkCore;

namespace Dzaba.HomeSecurity.Org.Service.Services;

/// <summary>
/// Org.Service owns the Membership/UserRole/RolePermission tables
/// directly, so its IPermissionSourceLoader implementation just queries
/// them - a service that doesn't own this data (e.g. Devices.Service)
/// would call Org.Service's access-context API instead. See
/// PermissionEvaluator (Dzaba.HomeSecurity.Authorization) for how this
/// result gets cached.
/// </summary>
internal sealed class LocalDbPermissionSourceLoader : IPermissionSourceLoader
{
    private readonly DbContextOptions<AppDbContext> dbOptions;

    public LocalDbPermissionSourceLoader(DbContextOptions<AppDbContext> dbOptions)
    {
        ArgumentNullException.ThrowIfNull(dbOptions);

        this.dbOptions = dbOptions;
    }

    public async Task<AccessContext> LoadAsync(Guid tenantId, string userId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(userId);

        // A fresh AppDbContext bound to the tenant being looked up, not the
        // ambient request tenant - keeps this loader callable for any
        // (tenant, user) pair (e.g. cache invalidation fan-out over other
        // users) and independently unit-testable.
        using var dbContext = new AppDbContext(dbOptions, new StaticTenantContext(tenantId));

        var isMember = await dbContext.Memberships
            .AnyAsync(m => m.OrganizationId == tenantId && m.UserId == userId, cancellationToken)
            .ConfigureAwait(false);

        if (!isMember)
        {
            return new AccessContext(false, []);
        }

        var permissionKeys = await dbContext.UserRoles
            .Where(ur => ur.TenantId == tenantId && ur.UserId == userId)
            .SelectMany(ur => ur.Role.RolePermissions)
            .Select(rp => rp.PermissionKey)
            .Distinct()
            .ToArrayAsync(cancellationToken)
            .ConfigureAwait(false);

        return new AccessContext(true, permissionKeys);
    }
}
