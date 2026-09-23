namespace Dzaba.HomeSecurity.Caching.Contracts;

/// <summary>
/// Cache-provider-agnostic abstraction over the small set of Set-based
/// cache operations this codebase actually needs
/// (docs/architecture/07-caching-and-idempotency.md). Domain/application
/// code depends on this only - never on a specific cache client - so a
/// service can swap its cache implementation without touching anything
/// above this seam. Mirrors IMessageBus's narrow, purpose-fit shape rather
/// than wrapping a generic cache API.
/// </summary>
public interface ICacheClient
{
    /// <exception cref="CacheUnavailableException">The cache is unreachable.</exception>
    Task<bool> KeyExistsAsync(string key, CancellationToken cancellationToken = default);

    /// <exception cref="CacheUnavailableException">The cache is unreachable.</exception>
    Task<bool> SetContainsAsync(string key, string value, CancellationToken cancellationToken = default);

    /// <exception cref="CacheUnavailableException">The cache is unreachable.</exception>
    Task SetAddAsync(string key, IReadOnlyCollection<string> values, CancellationToken cancellationToken = default);

    /// <exception cref="CacheUnavailableException">The cache is unreachable.</exception>
    Task KeyExpireAsync(string key, TimeSpan ttl, CancellationToken cancellationToken = default);

    /// <exception cref="CacheUnavailableException">The cache is unreachable.</exception>
    Task KeyDeleteAsync(string key, CancellationToken cancellationToken = default);
}
