using System.Text.Json;
using Dzaba.HomeSecurity.DomainEvents;
using Dzaba.HomeSecurity.MessageBroker.Contracts;

namespace Dzaba.HomeSecurity.Audit.Service.Ingestion;

/// <summary>
/// Reads a message off the queue and hands it to <see cref="AuditEventIngestor"/>,
/// deciding what should happen to the message itself: stored or already stored
/// is done, a transient failure is tried again, and a message that can never be
/// stored is sent to the dead-letter queue instead of being retried in vain.
/// </summary>
internal sealed class AuditEventHandler : IMessageHandler
{
    private readonly AuditEventIngestor ingestor;
    private readonly ILogger<AuditEventHandler> logger;

    public AuditEventHandler(AuditEventIngestor ingestor, ILogger<AuditEventHandler> logger)
    {
        ArgumentNullException.ThrowIfNull(ingestor);
        ArgumentNullException.ThrowIfNull(logger);

        this.ingestor = ingestor;
        this.logger = logger;
    }

    public async Task<MessageOutcome> HandleAsync(string routingKey, ReadOnlyMemory<byte> body, CancellationToken cancellationToken)
    {
        DomainEventMessage? message;
        try
        {
            message = JsonSerializer.Deserialize<DomainEventMessage>(body.Span);
        }
        catch (JsonException ex)
        {
            // Never the body itself: an event's extended data is exactly what
            // must not end up in an operational log.
            logger.LogWarning(ex, "A {RoutingKey} message is not valid JSON for a domain event, dead-lettering it", routingKey);
            return MessageOutcome.Reject;
        }

        var problem = DomainEventValidator.Validate(message, routingKey);
        if (problem is not null)
        {
            logger.LogWarning("A {RoutingKey} message is not a storable domain event ({Problem}), dead-lettering it", routingKey, problem);
            return MessageOutcome.Reject;
        }

        try
        {
            var result = await ingestor.IngestAsync(message!, cancellationToken).ConfigureAwait(false);
            logger.LogInformation("Domain event {EventId} ({RoutingKey}): {Result}", message!.EventId, routingKey, result);
            return MessageOutcome.Acknowledge;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Shutting down mid-message: leave it for the next instance.
            return MessageOutcome.Retry;
        }
        catch (Exception ex)
        {
            // The database being down or busy is the realistic cause. The
            // broker's delivery limit is the backstop if it never clears.
            logger.LogError(ex, "Could not store domain event {EventId} ({RoutingKey}), will retry", message!.EventId, routingKey);
            return MessageOutcome.Retry;
        }
    }
}
