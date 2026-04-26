using Dzaba.HomeSecurity.LogsIngestion.Contracts;
using Dzaba.HomeSecurity.MessageBroker.Contracts;
using Microsoft.Extensions.Logging;

namespace Dzaba.HomeSecurity.MessageBroker.RabbitMQ;

internal sealed class RabbitMQEventPublisher : IEventPublisher
{
    private readonly ILogger<RabbitMQEventPublisher> logger;

    public RabbitMQEventPublisher(ILogger<RabbitMQEventPublisher> logger)
    {
        ArgumentNullException.ThrowIfNull(logger);

        this.logger = logger;
    }

    public Task PublishAsync(Events evt)
    {
        ArgumentNullException.ThrowIfNull(evt);

        throw new NotImplementedException();
    }
}
