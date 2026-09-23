namespace Dzaba.HomeSecurity.Caching.Contracts;

/// <summary>
/// Thrown by an <see cref="ICacheClient"/> implementation when the
/// underlying cache is unreachable, so callers can fail open (e.g. to the
/// permission source of truth) without depending on a specific cache
/// provider's own exception type.
/// </summary>
public sealed class CacheUnavailableException : Exception
{
    public CacheUnavailableException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
