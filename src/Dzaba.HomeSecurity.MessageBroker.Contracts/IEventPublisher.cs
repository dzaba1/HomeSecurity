using Dzaba.HomeSecurity.LogsIngestion.Contracts;

namespace Dzaba.HomeSecurity.MessageBroker.Contracts;

public interface IEventPublisher
{
    Task PublishAsync(Events evt);
}
