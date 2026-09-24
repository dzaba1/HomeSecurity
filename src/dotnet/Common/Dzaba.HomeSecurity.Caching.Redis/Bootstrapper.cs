using Dzaba.HomeSecurity.Caching.Contracts;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.DataProtection.KeyManagement;
using Microsoft.AspNetCore.DataProtection.StackExchangeRedis;
using Microsoft.Extensions.DependencyInjection;
using StackExchange.Redis;

namespace Dzaba.HomeSecurity.Caching.Redis;

/// <summary>
/// The only place StackExchange.Redis-specific wiring happens - callers
/// depend on ICacheClient (from Dzaba.HomeSecurity.Caching.Contracts) and
/// this extension method, never on StackExchange.Redis types directly.
/// The registered IConnectionMultiplexer stays available for callers that
/// genuinely need the concrete client (e.g. ASP.NET Core Data Protection's
/// Redis-backed key ring), not just this abstraction.
/// </summary>
public static class Bootstrapper
{
    public static IServiceCollection AddDzabaHomeSecurityRedisCache(this IServiceCollection services,
        Func<IServiceProvider, string> connectionStringFactory, params string[] healthCheckTags)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(connectionStringFactory);
        ArgumentNullException.ThrowIfNull(healthCheckTags);

        services.AddSingleton<IConnectionMultiplexer>(sp =>
        {
            var connectionString = connectionStringFactory(sp);
            var options = ConfigurationOptions.Parse(connectionString);
            // Fail open on a Redis outage (docs/architecture/07-caching-and-idempotency.md):
            // the default abortConnect=true would make Connect() throw synchronously
            // for the first caller - and every caller after it, since a failed
            // singleton factory isn't cached - until Redis is reachable again.
            // AbortOnConnectFail=false returns a multiplexer immediately and retries
            // in the background instead; still-disconnected calls surface as
            // per-call RedisExceptions, which RedisCacheClient translates into
            // CacheUnavailableException.
            options.AbortOnConnectFail = false;
            return ConnectionMultiplexer.Connect(options);
        });
        services.AddTransient<ICacheClient, RedisCacheClient>();
        services.AddHealthChecks()
            .AddRedis(sp => sp.GetRequiredService<IConnectionMultiplexer>(), name: "redis", tags: healthCheckTags);

        return services;
    }

    /// <summary>
    /// Persists ASP.NET Core Data Protection's key ring in Redis, so every
    /// replica behind a rolling deployment can decrypt what another replica
    /// encrypted without a separate secrets store. Reuses the singleton
    /// IConnectionMultiplexer <see cref="AddDzabaHomeSecurityRedisCache"/>
    /// registers (resolved lazily, on first use of the key ring), so that
    /// must be registered too. Unlike the cache, the key ring has no
    /// fallback store: a Redis outage blocks encrypting and decrypting, so a
    /// service using this should keep the Redis health check a hard readiness
    /// dependency (no "degraded" tag).
    /// </summary>
    /// <param name="applicationName">
    /// The data-protection application discriminator: services sharing one
    /// key ring must use the same value, isolated services a distinct one.
    /// </param>
    /// <param name="keyRingName">The Redis key the key ring is stored under.</param>
    public static IServiceCollection AddDzabaHomeSecurityRedisDataProtection(this IServiceCollection services,
        string applicationName, string keyRingName)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrEmpty(applicationName);
        ArgumentException.ThrowIfNullOrEmpty(keyRingName);

        services.AddDataProtection().SetApplicationName(applicationName);
        services.AddOptions<KeyManagementOptions>().Configure<IConnectionMultiplexer>((options, redis) =>
            options.XmlRepository = new RedisXmlRepository(() => redis.GetDatabase(), keyRingName));

        return services;
    }
}
