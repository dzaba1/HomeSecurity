using System.Diagnostics;
using Dzaba.HomeSecurity.MessageBroker.Contracts;
using EasyNetQ;
using Microsoft.Extensions.Logging;

namespace Dzaba.HomeSecurity.MessageBroker.RabbitMQ;

internal sealed class RabbitMqMessageBus : IMessageBus
{
    private readonly ILogger<RabbitMqMessageBus> logger;
    private readonly IBus bus;

    public RabbitMqMessageBus(ILogger<RabbitMqMessageBus> logger, IBus bus)
    {
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(bus);

        this.logger = logger;
        this.bus = bus;
    }

    public async Task PublishAsync<TMessage>(string routingKey, TMessage message, CancellationToken cancellationToken = default)
        where TMessage : class
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(routingKey);
        ArgumentNullException.ThrowIfNull(message);

        logger.LogDebug("Publishing {MessageType} with routing key {RoutingKey}", typeof(TMessage).Name, routingKey);

        var startTimestamp = Stopwatch.GetTimestamp();
        // Not bus.PubSub: it names the exchange after the .NET message type,
        // which would tie every non-.NET consumer to a C# type name. All
        // events go to one explicitly named topic exchange instead.
        await bus.Advanced
            .ExchangeDeclareAsync(ExchangeNames.Events, c => c.AsDurable(true).WithType(ExchangeType.Topic), cancellationToken)
            .ConfigureAwait(false);
        var envelope = new Message<TMessage>(message, new MessageProperties { DeliveryMode = MessageDeliveryMode.Persistent });
        await bus.Advanced
            .PublishAsync(ExchangeNames.Events, routingKey, false, envelope, cancellationToken)
            .ConfigureAwait(false);

        logger.LogInformation("Published {MessageType} with routing key {RoutingKey} in {Elapsed}",
            typeof(TMessage).Name, routingKey, Stopwatch.GetElapsedTime(startTimestamp));
    }
}
