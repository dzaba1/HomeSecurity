using Dzaba.HomeSecurity.Domain;

namespace Dzaba.HomeSecurity.Audit.Service.Data.Entities;

/// <summary>
/// One recorded domain event: who did what, to which tenant's data, and
/// when. Insert-only by design (the service's database role has no UPDATE or
/// DELETE on this table - see the InitialCreate migration): a stored event is
/// never modified, so the hash chain over it stays valid and erasing a person
/// means deleting their <see cref="ActorPseudonym"/> row, not rewriting
/// events. See docs/architecture/16-auditing-and-compliance.md.
/// </summary>
internal sealed class AuditEvent : ITenantOwned
{
    /// <summary>
    /// The order events were stored in - what the hash chains are ordered by.
    /// Assigned by the database.
    /// </summary>
    public long Seq { get; set; }

    /// <summary>
    /// The event's own id from the envelope. Unique: a redelivered message
    /// hits the index and is dropped instead of double-recorded.
    /// </summary>
    public Guid EventId { get; set; }

    /// <summary>
    /// When the publishing service says the action happened, truncated to
    /// whole microseconds (see <see cref="AuditEventHasher.TruncateToMicroseconds"/>)
    /// so what is hashed is exactly what the database stores.
    /// </summary>
    public DateTimeOffset OccurredAt { get; set; }

    /// <summary>
    /// When this service stored it. Together with <see cref="OccurredAt"/> it
    /// shows delivery lag or clock skew.
    /// </summary>
    public DateTimeOffset ReceivedAt { get; set; }

    /// <summary>Null for pre-tenant events such as a login.</summary>
    public Guid? TenantId { get; set; }

    public AuditCategory Category { get; set; }

    public AuditActorType ActorType { get; set; }

    /// <summary>
    /// An opaque token standing in for the actor's real id, resolved through
    /// <see cref="ActorPseudonym"/>. The real id is never stored on the event.
    /// </summary>
    public Guid ActorRef { get; set; }

    /// <summary>The dotted event name, e.g. <c>role.assigned</c>.</summary>
    public required string Action { get; set; }

    public required string TargetType { get; set; }

    public required string TargetId { get; set; }

    /// <summary>
    /// The event's extended data as the exact JSON text it arrived as. Stored
    /// as json, not jsonb, on purpose: jsonb normalises key order and
    /// whitespace, which would make the text - and so the hash computed over
    /// it - differ after a round trip.
    /// </summary>
    public required string Metadata { get; set; }

    /// <summary>The previous event's <see cref="Hash"/> in this event's chain; empty for the first.</summary>
    public required byte[] PrevHash { get; set; }

    /// <summary>SHA-256 over <see cref="PrevHash"/> and this event's fields - see <see cref="AuditEventHasher"/>.</summary>
    public required byte[] Hash { get; set; }
}
