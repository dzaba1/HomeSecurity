namespace Dzaba.HomeSecurity.MessageBroker.RabbitMQ;

/// <summary>
/// Broker-level names that are part of the cross-service contract. Consumers,
/// in any language, bind their own queues to <see cref="Events"/> by routing
/// key - the name deliberately doesn't derive from a .NET type name, which is
/// what EasyNetQ's default PubSub convention would do.
/// </summary>
public static class ExchangeNames
{
    /// <summary>
    /// The single durable topic exchange every domain event is published to;
    /// the routing key (e.g. "logs.ingested") says which event it is.
    /// </summary>
    public const string Events = "homesecurity.events";
}
