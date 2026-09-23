namespace Dzaba.HomeSecurity.Authorization;

/// <summary>
/// Configuration for the shared access-context cache
/// (docs/architecture/07-caching-and-idempotency.md). Bound from the
/// "PermissionCache" configuration section.
/// </summary>
public sealed class PermissionCacheOptions
{
    public const string SectionName = "PermissionCache";

    /// <summary>
    /// Safety-net TTL, in minutes - invalidation is explicit on every write
    /// that can change a user's effective access, so this only guards
    /// against a missed invalidation, not the primary mechanism.
    /// </summary>
    public int TtlMinutes { get; set; } = 15;
}
