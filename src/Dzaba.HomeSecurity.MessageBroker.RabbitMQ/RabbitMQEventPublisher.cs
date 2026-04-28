using Dzaba.HomeSecurity.LogsIngestion.Contracts;
using Dzaba.HomeSecurity.MessageBroker.Contracts;
using EasyNetQ;
using Microsoft.Extensions.Logging;

namespace Dzaba.HomeSecurity.MessageBroker.RabbitMQ;

internal sealed class RabbitMQEventPublisher : IEventPublisher
{
    private readonly ILogger<RabbitMQEventPublisher> logger;
    private readonly IBus bus;

    public RabbitMQEventPublisher(ILogger<RabbitMQEventPublisher> logger,
        IBus bus)
    {
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(bus);

        this.logger = logger;
        this.bus = bus;
    }

    public Task PublishAsync(Events evt)
    {
        ArgumentNullException.ThrowIfNull(evt);

        throw new NotImplementedException();
    }
}
