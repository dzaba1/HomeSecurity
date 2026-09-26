using Dzaba.HomeSecurity.Domain;
using Dzaba.HomeSecurity.MessageBroker.Contracts;
using Microsoft.Extensions.Logging;

namespace Dzaba.HomeSecurity.DomainEvents;

internal sealed class DomainEventPublisher : IDomainEventPublisher
{
    private readonly IMessageBus messageBus;
    private readonly ICurrentActor currentActor;
    private readonly ITenantContext tenantContext;
    private readonly ILogger<DomainEventPublisher> logger;

    public DomainEventPublisher(IMessageBus messageBus, ICurrentActor currentActor, ITenantContext tenantContext,
        ILogger<DomainEventPublisher> logger)
    {
        ArgumentNullException.ThrowIfNull(messageBus);
        ArgumentNullException.ThrowIfNull(currentActor);
        ArgumentNullException.ThrowIfNull(tenantContext);
        ArgumentNullException.ThrowIfNull(logger);

        this.messageBus = messageBus;
        this.currentActor = currentActor;
        this.tenantContext = tenantContext;
        this.logger = logger;
    }

    public async Task<Guid> PublishAsync(
        string eventName,
        string targetType,
        string targetId,
        IReadOnlyDictionary<string, object?>? metadata = null,
        Guid? tenantId = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(eventName);
        ArgumentException.ThrowIfNullOrWhiteSpace(targetType);
        ArgumentException.ThrowIfNullOrWhiteSpace(targetId);

        var resolvedTenantId = tenantId ?? tenantContext.TenantId;
        var message = new DomainEventMessage
        {
            EventId = Guid.NewGuid(),
            OccurredAt = DateTimeOffset.UtcNow,
            // Guid.Empty is the tenant context's "no tenant" value (routes
            // without an orgId), which the envelope spells as null.
            TenantId = resolvedTenantId == Guid.Empty ? null : resolvedTenantId,
            Actor = currentActor.GetCurrent(),
            Action = eventName,
            Target = new Target { Type = targetType, Id = targetId },
            Metadata = metadata ?? new Dictionary<string, object?>()
        };

        // Identifiers only: the metadata is deliberately never logged.
        logger.LogDebug("Publishing domain event {EventName} {EventId} on {TargetType} {TargetId} by {ActorType} {ActorId} in tenant {TenantId}",
            eventName, message.EventId, targetType, targetId, message.Actor.Type, message.Actor.Id, message.TenantId);

        await messageBus.PublishAsync(eventName, message, cancellationToken).ConfigureAwait(false);

        logger.LogInformation("Published domain event {EventName} {EventId} in tenant {TenantId}",
            eventName, message.EventId, message.TenantId);

        return message.EventId;
    }
}
