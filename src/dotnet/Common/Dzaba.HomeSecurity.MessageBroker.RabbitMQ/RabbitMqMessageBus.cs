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
        await bus.PubSub.PublishAsync(message, routingKey, cancellationToken).ConfigureAwait(false);

        logger.LogInformation("Published {MessageType} with routing key {RoutingKey} in {Elapsed}",
            typeof(TMessage).Name, routingKey, Stopwatch.GetElapsedTime(startTimestamp));
    }
}
