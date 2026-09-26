namespace Dzaba.HomeSecurity.Audit.Service.Data.Entities;

/// <summary>
/// The tip of one hash chain: the hash of the last event stored for a
/// (tenant, category). Storing an event moves the tip in the same transaction
/// that inserts it, and <see cref="LastHash"/> is a concurrency token, so the
/// update only succeeds if the tip is still the one the new event chained onto.
/// That serializes writers per chain without a database-specific lock: with
/// several instances of the service consuming at once, exactly one of two
/// racing events wins and the loser re-reads the tip and retries, so a chain
/// never forks. The only table besides <see cref="ActorPseudonym"/> the writer
/// role may UPDATE.
/// </summary>
internal sealed class ChainHead
{
    /// <summary>
    /// The chain's tenant; <see cref="Guid.Empty"/> for events with no tenant,
    /// so it can be part of a non-nullable key.
    /// </summary>
    public Guid TenantId { get; set; }

    public AuditCategory Category { get; set; }

    /// <summary>The hash of the chain's newest event; what the next event's <c>PrevHash</c> must be.</summary>
    public required byte[] LastHash { get; set; }
}
