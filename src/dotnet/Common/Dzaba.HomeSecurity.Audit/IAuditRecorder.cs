namespace Dzaba.HomeSecurity.Audit;

/// <summary>
/// Records an audit event as part of the caller's own unit of work: the event
/// is added to the service's DbContext (the outbox table) and is persisted by
/// the same <c>SaveChangesAsync</c> as the change it describes, so the change
/// and its audit record commit or roll back together. Nothing is published
/// here - <see cref="AuditOutboxRelay{TContext}"/> does that afterwards.
/// </summary>
public interface IAuditRecorder
{
    /// <param name="action">One of <see cref="AuditActions"/>.</param>
    /// <param name="targetType">One of <see cref="AuditTargetTypes"/>.</param>
    /// <param name="targetId">The id of the thing acted on.</param>
    /// <param name="metadata">
    /// Action-specific detail built by the caller from an explicit whitelist
    /// of identifiers and before/after values - never from a request or
    /// entity object, and never a secret (see
    /// docs/architecture/16-auditing-and-compliance.md section 2).
    /// </param>
    /// <param name="tenantId">
    /// The tenant the action belongs to. Defaults to the current request's
    /// tenant; pass it explicitly when that is not the tenant being acted on
    /// (e.g. creating an organization).
    /// </param>
    /// <returns>The new event's id.</returns>
    Guid Record(
        string action,
        string targetType,
        string targetId,
        IReadOnlyDictionary<string, object?>? metadata = null,
        Guid? tenantId = null);
}
