using System.Diagnostics;
using Dzaba.HomeSecurity.Caching.Contracts;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace Dzaba.HomeSecurity.Caching.Redis;

/// <summary>
/// The only place StackExchange.Redis-specific wiring happens - callers
/// depend on ICacheClient (from Dzaba.HomeSecurity.Caching.Contracts) and
/// Bootstrapper, never on StackExchange.Redis types directly. Every
/// RedisException is logged and translated into CacheUnavailableException
/// so callers can fail open without a Redis-specific dependency.
/// </summary>
internal sealed class RedisCacheClient : ICacheClient
{
    private readonly IConnectionMultiplexer redis;
    private readonly ILogger<RedisCacheClient> logger;

    public RedisCacheClient(IConnectionMultiplexer redis, ILogger<RedisCacheClient> logger)
    {
        ArgumentNullException.ThrowIfNull(redis);
        ArgumentNullException.ThrowIfNull(logger);

        this.redis = redis;
        this.logger = logger;
    }

    public Task<bool> KeyExistsAsync(string key, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(key);

        return RunAsync(() => redis.GetDatabase().KeyExistsAsync(key), nameof(KeyExistsAsync), key);
    }

    public Task<bool> SetContainsAsync(string key, string value, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(key);
        ArgumentException.ThrowIfNullOrEmpty(value);

        return RunAsync(() => redis.GetDatabase().SetContainsAsync(key, value), nameof(SetContainsAsync), key);
    }

    public Task SetAddAsync(string key, IReadOnlyCollection<string> values, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(key);
        ArgumentNullException.ThrowIfNull(values);

        var redisValues = Array.ConvertAll([.. values], v => (RedisValue)v);
        return RunAsync(() => redis.GetDatabase().SetAddAsync(key, redisValues), nameof(SetAddAsync), key);
    }

    public Task KeyExpireAsync(string key, TimeSpan ttl, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(key);

        return RunAsync(() => redis.GetDatabase().KeyExpireAsync(key, ttl), nameof(KeyExpireAsync), key);
    }

    public Task KeyDeleteAsync(string key, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(key);

        return RunAsync(() => redis.GetDatabase().KeyDeleteAsync(key), nameof(KeyDeleteAsync), key);
    }

    private async Task<T> RunAsync<T>(Func<Task<T>> operation, string operationName, string key)
    {
        logger.LogDebug("Starting {Operation} for key {Key}", operationName, key);
        var startTimestamp = Stopwatch.GetTimestamp();

        try
        {
            var result = await operation().ConfigureAwait(false);

            logger.LogDebug("Completed {Operation} for key {Key} in {Elapsed}",
                operationName, key, Stopwatch.GetElapsedTime(startTimestamp));

            return result;
        }
        catch (RedisException ex)
        {
            logger.LogError(ex, "Redis unavailable during {Operation} for key {Key}", operationName, key);
            throw new CacheUnavailableException($"Redis was unreachable during {operationName}.", ex);
        }
    }

    private async Task RunAsync(Func<Task> operation, string operationName, string key)
    {
        logger.LogDebug("Starting {Operation} for key {Key}", operationName, key);
        var startTimestamp = Stopwatch.GetTimestamp();

        try
        {
            await operation().ConfigureAwait(false);

            logger.LogDebug("Completed {Operation} for key {Key} in {Elapsed}",
                operationName, key, Stopwatch.GetElapsedTime(startTimestamp));
        }
        catch (RedisException ex)
        {
            logger.LogError(ex, "Redis unavailable during {Operation} for key {Key}", operationName, key);
            throw new CacheUnavailableException($"Redis was unreachable during {operationName}.", ex);
        }
    }
}
