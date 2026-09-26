namespace Dzaba.HomeSecurity.MessageBroker.Contracts;

/// <summary>
/// Broker-agnostic consume abstraction, the counterpart of <see cref="IMessageBus"/>:
/// a service implements this for the messages it subscribes to and never
/// touches a broker client. It receives the raw body, not a deserialized type,
/// so a handler decides how strictly to read what a publisher sent (and can
/// report a malformed message as <see cref="MessageOutcome.Reject"/> instead
/// of the broker library throwing).
/// </summary>
public interface IMessageHandler
{
    /// <param name="routingKey">The routing key the message was published under - for a domain event, its name.</param>
    /// <param name="body">The message body, valid only for the duration of this call.</param>
    Task<MessageOutcome> HandleAsync(string routingKey, ReadOnlyMemory<byte> body, CancellationToken cancellationToken);
}
