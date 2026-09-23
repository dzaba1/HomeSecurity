using Dzaba.HomeSecurity.Data;
using Dzaba.HomeSecurity.Domain;
using Microsoft.EntityFrameworkCore;

namespace Dzaba.HomeSecurity.Org.Service.Tenancy;

/// <summary>
/// Org.Service owns Membership directly, so tenant resolution just queries
/// it - a service that doesn't own that data answers from the shared
/// access-context cache instead (see
/// Bootstrapper.AddDzabaHomeSecurityCachedTenantMembership).
/// </summary>
internal sealed class DbTenantMembershipChecker : ITenantMembershipChecker
{
    private readonly DbContextOptions<AppDbContext> dbOptions;

    public DbTenantMembershipChecker(DbContextOptions<AppDbContext> dbOptions)
    {
        ArgumentNullException.ThrowIfNull(dbOptions);

        this.dbOptions = dbOptions;
    }

    public async Task<bool> IsMemberAsync(Guid tenantId, string userId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(userId);

        // A throwaway AppDbContext bound to the claimed org - the request's
        // real (ambient-tenant) AppDbContext can't be used here, because the
        // ambient tenant hasn't been resolved yet (that's what this check
        // decides). Same escape-hatch pattern as DataTestFixture.CreateContext.
        using var checkDb = new AppDbContext(dbOptions, new StaticTenantContext(tenantId));

        return await checkDb.Memberships
            .AnyAsync(m => m.OrganizationId == tenantId && m.UserId == userId, cancellationToken)
            .ConfigureAwait(false);
    }
}
