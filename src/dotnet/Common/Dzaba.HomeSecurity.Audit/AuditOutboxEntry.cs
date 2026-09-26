namespace Dzaba.HomeSecurity.Audit;

/// <summary>
/// One audit event waiting to be published: written in the same database
/// transaction as the change it describes (transactional outbox, see
/// docs/architecture/16-auditing-and-compliance.md section 3), then deleted by
/// <see cref="AuditOutboxRelay{TContext}"/> once RabbitMQ has accepted it.
/// Deliberately not tenant-owned: the relay drains every tenant's rows, and
/// the tenant is already inside <see cref="Payload"/>.
/// </summary>
public class AuditOutboxEntry
{
    /// <summary>The event's own id (<c>eventId</c> in the envelope).</summary>
    public Guid Id { get; set; }

    public string RoutingKey { get; set; } = string.Empty;

    /// <summary>The serialized <c>AuditEventMessage</c>.</summary>
    public string Payload { get; set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; set; }
}
