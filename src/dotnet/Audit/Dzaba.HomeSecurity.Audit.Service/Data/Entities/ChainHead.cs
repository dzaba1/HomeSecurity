namespace Dzaba.HomeSecurity.Audit.Service.Data.Entities;

/// <summary>
/// The tip of one hash chain: the last event stored for a (tenant, category).
/// Inserting an event locks this row inside the insert transaction, which
/// serializes writers per chain so each new event chains onto exactly one
/// predecessor even with several instances of the service consuming at once.
/// The only table besides <see cref="ActorPseudonym"/> the writer role may
/// UPDATE.
/// </summary>
internal sealed class ChainHead
{
    /// <summary>
    /// The chain's tenant; <see cref="Guid.Empty"/> for events with no tenant,
    /// so it can be part of a non-nullable key.
    /// </summary>
    public Guid TenantId { get; set; }

    public AuditCategory Category { get; set; }

    public long LastSeq { get; set; }

    public required byte[] LastHash { get; set; }
}
