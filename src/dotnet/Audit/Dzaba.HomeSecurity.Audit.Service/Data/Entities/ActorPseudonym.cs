namespace Dzaba.HomeSecurity.Audit.Service.Data.Entities;

/// <summary>
/// The one place an actor's real id is linked to the opaque token events
/// carry. Created the first time an actor is seen and deliberately the only
/// mutable, deletable table: erasing a person deletes their row here, which
/// severs every event from them without touching an event (so the append-only
/// grants and the hash chains stay valid). The read API renders an event whose
/// token has no row as an erased actor.
/// </summary>
/// <remarks>
/// Not tenant-owned: a person acting in several organizations has one token,
/// so erasing them is one delete. It is therefore never queried on its own by
/// a tenant-facing endpoint - only joined from that tenant's own events, so a
/// tenant can resolve just the actors that appear in its trail.
/// </remarks>
internal sealed class ActorPseudonym
{
    public AuditActorType ActorType { get; set; }

    /// <summary>Keycloak subject for a user, agent credential id for a device.</summary>
    public required string ActorId { get; set; }

    public Guid Token { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
}
