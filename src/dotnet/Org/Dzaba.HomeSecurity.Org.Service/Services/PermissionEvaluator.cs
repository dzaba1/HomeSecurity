using Dzaba.HomeSecurity.Data;
using Dzaba.HomeSecurity.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace Dzaba.HomeSecurity.Org.Service.Services;

internal sealed class PermissionEvaluator : IPermissionEvaluator
{
    // Real permission keys are always "noun.verb" shaped (see
    // Dzaba.HomeSecurity.Domain.PermissionKeys), so this can never collide.
    // Needed because Redis deletes a Set once its last member is removed,
    // which would otherwise make "cached and genuinely empty" indistinguishable
    // from "never cached".
    private const string NoneSentinel = "__none__";

    private readonly IConnectionMultiplexer redis;
    private readonly DbContextOptions<AppDbContext> dbOptions;
    private readonly PermissionCacheOptions options;
    private readonly ILogger<PermissionEvaluator> logger;

    public PermissionEvaluator(IConnectionMultiplexer redis, DbContextOptions<AppDbContext> dbOptions,
        IOptions<PermissionCacheOptions> options, ILogger<PermissionEvaluator> logger)
    {
        ArgumentNullException.ThrowIfNull(redis);
        ArgumentNullException.ThrowIfNull(dbOptions);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(logger);

        this.redis = redis;
        this.dbOptions = dbOptions;
        this.options = options.Value;
        this.logger = logger;
    }

    public async Task<bool> HasPermissionAsync(string userId, Guid tenantId, string permissionKey, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(userId);
        ArgumentException.ThrowIfNullOrEmpty(permissionKey);

        try
        {
            var db = redis.GetDatabase();
            var key = CacheKey(tenantId, userId);

            if (!await db.KeyExistsAsync(key).ConfigureAwait(false))
            {
                await PopulateAsync(db, key, tenantId, userId, cancellationToken).ConfigureAwait(false);
            }

            return await db.SetContainsAsync(key, permissionKey).ConfigureAwait(false);
        }
        catch (RedisException ex)
        {
            // Redis is a cache, not the source of truth (docs/architecture/07)
            // - fail open to Postgres directly rather than failing the
            // request, at the cost of one extra DB join per call for as long
            // as Redis stays unreachable. Logged as an error, not a warning:
            // this should never happen and someone should be paged.
            logger.LogError(ex, "Redis unavailable while checking permission {PermissionKey} for tenant {TenantId} user {UserId} - falling back to the database",
                permissionKey, tenantId, userId);
            var permissionKeys = await LoadPermissionKeysAsync(tenantId, userId, cancellationToken).ConfigureAwait(false);
            return Array.IndexOf(permissionKeys, permissionKey) >= 0;
        }
    }

    public async Task InvalidateAsync(Guid tenantId, string userId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(userId);

        try
        {
            var db = redis.GetDatabase();
            await db.KeyDeleteAsync(CacheKey(tenantId, userId)).ConfigureAwait(false);

            logger.LogDebug("Invalidated permission cache for tenant {TenantId} user {UserId}", tenantId, userId);
        }
        catch (RedisException ex)
        {
            // Nothing to invalidate if Redis can't be reached - HasPermissionAsync
            // is already bypassing the cache in this state, and the TTL set in
            // PopulateAsync bounds the staleness once Redis comes back.
            logger.LogError(ex, "Redis unavailable while invalidating permission cache for tenant {TenantId} user {UserId}", tenantId, userId);
        }
    }

    private async Task PopulateAsync(IDatabase db, RedisKey key, Guid tenantId, string userId, CancellationToken cancellationToken)
    {
        var permissionKeys = await LoadPermissionKeysAsync(tenantId, userId, cancellationToken).ConfigureAwait(false);

        var values = permissionKeys.Length == 0
            ? [(RedisValue)NoneSentinel]
            : Array.ConvertAll(permissionKeys, k => (RedisValue)k);

        await db.SetAddAsync(key, values).ConfigureAwait(false);
        await db.KeyExpireAsync(key, TimeSpan.FromMinutes(options.TtlMinutes)).ConfigureAwait(false);

        logger.LogDebug("Permission cache miss for tenant {TenantId} user {UserId}, populated {Count} permission(s)",
            tenantId, userId, permissionKeys.Length);
    }

    private async Task<string[]> LoadPermissionKeysAsync(Guid tenantId, string userId, CancellationToken cancellationToken)
    {
        // A fresh AppDbContext bound to the tenant being looked up, not the
        // ambient request tenant - keeps this evaluator callable for any
        // (tenant, user) pair (e.g. invalidation fan-out over other users)
        // and independently unit-testable.
        using var dbContext = new AppDbContext(dbOptions, new StaticTenantContext(tenantId));

        return await dbContext.UserRoles
            .Where(ur => ur.TenantId == tenantId && ur.UserId == userId)
            .SelectMany(ur => ur.Role.RolePermissions)
            .Select(rp => rp.PermissionKey)
            .Distinct()
            .ToArrayAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    private static RedisKey CacheKey(Guid tenantId, string userId) => $"perms:{tenantId}:{userId}";
}
