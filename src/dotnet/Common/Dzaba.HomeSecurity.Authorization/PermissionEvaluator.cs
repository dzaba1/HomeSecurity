using Dzaba.HomeSecurity.Caching.Contracts;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Dzaba.HomeSecurity.Authorization;

internal sealed class PermissionEvaluator : IPermissionEvaluator
{
    // Real permission keys are always "noun.verb" shaped and a real user id
    // is never empty, so these sentinels can never collide with a real
    // cached value. Needed because a cache Set is deleted once its last
    // member is removed, which would otherwise make "cached and genuinely
    // empty" indistinguishable from "never cached".
    private const string NoneSentinel = "__none__";
    private const string NotMemberSentinel = "__not_member__";

    private readonly ICacheClient cache;
    private readonly IPermissionSourceLoader sourceLoader;
    private readonly PermissionCacheOptions options;
    private readonly ILogger<PermissionEvaluator> logger;

    public PermissionEvaluator(ICacheClient cache, IPermissionSourceLoader sourceLoader,
        IOptions<PermissionCacheOptions> options, ILogger<PermissionEvaluator> logger)
    {
        ArgumentNullException.ThrowIfNull(cache);
        ArgumentNullException.ThrowIfNull(sourceLoader);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(logger);

        this.cache = cache;
        this.sourceLoader = sourceLoader;
        this.options = options.Value;
        this.logger = logger;
    }

    public async Task<bool> HasPermissionAsync(string userId, Guid tenantId, string permissionKey, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(userId);
        ArgumentException.ThrowIfNullOrEmpty(permissionKey);

        var key = CacheKey(tenantId, userId);

        try
        {
            await EnsurePopulatedAsync(key, tenantId, userId, cancellationToken).ConfigureAwait(false);
            return await cache.SetContainsAsync(key, permissionKey, cancellationToken).ConfigureAwait(false);
        }
        catch (CacheUnavailableException ex)
        {
            // The cache is an optimization, not the source of truth - fail
            // open to the permission source directly rather than failing
            // the request, at the cost of one extra round-trip per call for
            // as long as the cache stays unreachable. Logged as an error,
            // not a warning: this should never happen and someone should be
            // paged.
            logger.LogError(ex, "Cache unavailable while checking permission {PermissionKey} for tenant {TenantId} user {UserId} - falling back to the permission source",
                permissionKey, tenantId, userId);
            var accessContext = await sourceLoader.LoadAsync(tenantId, userId, cancellationToken).ConfigureAwait(false);
            return Array.IndexOf(accessContext.PermissionKeys, permissionKey) >= 0;
        }
    }

    public async Task<bool> IsMemberAsync(string userId, Guid tenantId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(userId);

        var key = CacheKey(tenantId, userId);

        try
        {
            await EnsurePopulatedAsync(key, tenantId, userId, cancellationToken).ConfigureAwait(false);
            return !await cache.SetContainsAsync(key, NotMemberSentinel, cancellationToken).ConfigureAwait(false);
        }
        catch (CacheUnavailableException ex)
        {
            logger.LogError(ex, "Cache unavailable while checking membership for tenant {TenantId} user {UserId} - falling back to the permission source",
                tenantId, userId);
            var accessContext = await sourceLoader.LoadAsync(tenantId, userId, cancellationToken).ConfigureAwait(false);
            return accessContext.IsMember;
        }
    }

    public async Task InvalidateAsync(Guid tenantId, string userId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(userId);

        try
        {
            await cache.KeyDeleteAsync(CacheKey(tenantId, userId), cancellationToken).ConfigureAwait(false);

            logger.LogDebug("Invalidated access-context cache for tenant {TenantId} user {UserId}", tenantId, userId);
        }
        catch (CacheUnavailableException ex)
        {
            // Nothing to invalidate if the cache can't be reached - the
            // other methods already bypass it in this state, and the TTL
            // set in EnsurePopulatedAsync bounds the staleness once it
            // recovers.
            logger.LogError(ex, "Cache unavailable while invalidating access-context cache for tenant {TenantId} user {UserId}", tenantId, userId);
        }
    }

    private async Task EnsurePopulatedAsync(string key, Guid tenantId, string userId, CancellationToken cancellationToken)
    {
        if (await cache.KeyExistsAsync(key, cancellationToken).ConfigureAwait(false))
        {
            return;
        }

        var accessContext = await sourceLoader.LoadAsync(tenantId, userId, cancellationToken).ConfigureAwait(false);

        string[] values = !accessContext.IsMember
            ? [NotMemberSentinel]
            : accessContext.PermissionKeys.Length == 0
                ? [NoneSentinel]
                : accessContext.PermissionKeys;

        await cache.SetAddAsync(key, values, cancellationToken).ConfigureAwait(false);
        await cache.KeyExpireAsync(key, TimeSpan.FromMinutes(options.TtlMinutes), cancellationToken).ConfigureAwait(false);

        logger.LogDebug("Access-context cache miss for tenant {TenantId} user {UserId}, populated {Count} permission(s), member={IsMember}",
            tenantId, userId, accessContext.PermissionKeys.Length, accessContext.IsMember);
    }

    private static string CacheKey(Guid tenantId, string userId) => $"perms:{tenantId}:{userId}";
}
