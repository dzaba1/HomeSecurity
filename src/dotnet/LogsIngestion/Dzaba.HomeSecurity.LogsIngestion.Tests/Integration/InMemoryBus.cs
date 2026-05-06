using EasyNetQ;

namespace Dzaba.HomeSecurity.LogsIngestion.Tests.Integration;

internal sealed class InMemoryBus : IBus
{
    public IPubSub PubSub => throw new NotImplementedException();

    public IRpc Rpc => throw new NotImplementedException();

    public ISendReceive SendReceive => throw new NotImplementedException();

    public IScheduler Scheduler => throw new NotImplementedException();

    public IAdvancedBus Advanced => throw new NotImplementedException();
}
