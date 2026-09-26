namespace Dzaba.HomeSecurity.MessageBroker.RabbitMQ;

/// <summary>
/// One service's subscription to the shared events exchange: its own queue,
/// and which routing keys (event names, or topic patterns like
/// <c>identity.#</c>) are bound to it. Same shape as the Go processor's
/// settings (ADR-0019), so the two consumers behave alike.
/// </summary>
public sealed class SubscriptionOptions
{
    /// <summary>
    /// The queue this service owns, e.g. <c>homesecurity.audit-service</c>.
    /// Its dead-letter exchange and queue are derived from it: <c>.dlx</c> and <c>.dlq</c>.
    /// </summary>
    public string Queue { get; set; } = string.Empty;

    /// <summary>The routing keys (or topic patterns) to bind to <see cref="ExchangeNames.Events"/>.</summary>
    public IReadOnlyList<string> RoutingKeys { get; set; } = [];

    /// <summary>How many unacknowledged messages the broker hands this consumer at once.</summary>
    public ushort PrefetchCount { get; set; } = 20;

    /// <summary>
    /// How many times a message may be delivered (the first attempt included)
    /// before the broker moves it to the dead-letter queue.
    /// </summary>
    public int DeliveryLimit { get; set; } = 5;
}
