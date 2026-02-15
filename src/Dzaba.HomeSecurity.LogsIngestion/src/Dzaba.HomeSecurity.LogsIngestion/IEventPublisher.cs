using Dzaba.HomeSecurity.LogsIngestion.Contracts;

namespace Dzaba.HomeSecurity.LogsIngestion;

public interface IEventPublisher
{
    Task PublishAsync(Events evt);
}
