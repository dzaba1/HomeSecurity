using System.Collections.Concurrent;
using Dzaba.HomeSecurity.MessageBroker.Contracts;

namespace Dzaba.HomeSecurity.TestUtils;

/// <summary>
/// Test double swapped in for the real RabbitMQ-backed IMessageBus in a
/// service's own test fixture - captures published messages for
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
