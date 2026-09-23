using System.Collections.Concurrent;
using Dzaba.HomeSecurity.MessageBroker.Contracts;

namespace Dzaba.HomeSecurity.LogsIngestion.Service.Tests;

/// <summary>
/// Test double swapped in for the real RabbitMQ-backed IMessageBus (see
/// LogsIngestionServiceTestFixture) - captures published messages for
/// assertions instead of needing a real broker for every controller test.
/// </summary>
public sealed class FakeMessageBus : IMessageBus
{
    public ConcurrentBag<(string RoutingKey, object Message)> Published { get; } = [];

    public Task PublishAsync<TMessage>(string routingKey, TMessage message, CancellationToken cancellationToken = default)
        where TMessage : class
    {
        Published.Add((routingKey, message));
        return Task.CompletedTask;
    }
}
