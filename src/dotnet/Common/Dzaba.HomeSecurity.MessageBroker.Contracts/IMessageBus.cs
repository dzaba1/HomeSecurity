namespace Dzaba.HomeSecurity.MessageBroker.Contracts;

/// <summary>
/// Broker-agnostic publish abstraction. Domain/application code depends on
/// this only - never on a specific broker client - so a service can swap
/// its message bus implementation (e.g. RabbitMQ for another broker)
/// without touching anything above this seam.
/// </summary>
public interface IMessageBus
{
    Task PublishAsync<TMessage>(string routingKey, TMessage message, CancellationToken cancellationToken = default)
        where TMessage : class;
}
