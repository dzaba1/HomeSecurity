using Dzaba.HomeSecurity.LogsIngestion.Contracts;

namespace Dzaba.HomeSecurity.MessageBroker.Contracts;

public interface IEventPublisher
{
    Task PublishAsync(int homeId, Events evt);
}
