namespace Dzaba.HomeSecurity.DomainEvents;

/// <summary>
/// Publishes one domain event for one operation a service has just carried
/// out, wrapped in the common envelope (event id, timestamp, tenant, actor,
/// target, extended data). The routing key is the event name itself, so any
/// number of services can subscribe to the events they care about; Audit.Service
/// is one of them. Published after the change has been saved, like the
/// service's other notifications.
/// </summary>
public interface IDomainEventPublisher
{
    /// <param name="eventName">One of <see cref="DomainEventNames"/>; also the routing key.</param>
    /// <param name="targetType">One of <see cref="DomainEventTargetTypes"/>.</param>
    /// <param name="targetId">The id of the thing acted on.</param>
    /// <param name="metadata">
    /// The extended data for this event, built by the caller from an explicit
    /// whitelist of identifiers and before/after values - never from a request
    /// or entity object, and never a secret (see
    /// docs/architecture/16-auditing-and-compliance.md section 2).
    /// </param>
    /// <param name="tenantId">
    /// The tenant the operation belongs to. Defaults to the current request's
    /// tenant; pass it explicitly when that is not the tenant being acted on
    /// (e.g. a device token's tenant on an endpoint with no {orgId} segment).
    /// </param>
    /// <returns>The published event's id.</returns>
    Task<Guid> PublishAsync(
        string eventName,
        string targetType,
        string targetId,
        IReadOnlyDictionary<string, object?>? metadata = null,
        Guid? tenantId = null,
        CancellationToken cancellationToken = default);
}
