using Dzaba.HomeSecurity.MessageBroker.Contracts;
using EasyNetQ;
using EasyNetQ.Consumer;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using IMessageHandler = Dzaba.HomeSecurity.MessageBroker.Contracts.IMessageHandler;

namespace Dzaba.HomeSecurity.MessageBroker.RabbitMQ;

/// <summary>
/// Consumes one service's queue and hands every message to its
/// <see cref="IMessageHandler"/>. The topology matches the Go processor's
/// (ADR-0019), so both consumers behave alike:
/// <code>
/// homesecurity.events --routing key--&gt; &lt;queue&gt; --(rejected / delivery limit)--&gt; &lt;queue&gt;.dlx --&gt; &lt;queue&gt;.dlq
/// </code>
/// The queue is a durable quorum queue: it supports a delivery limit, which is
/// what turns "retry" into "retry a few times, then park it in the DLQ" with no
/// retry code of our own. Reconnection after a broker restart is EasyNetQ's.
/// </summary>
internal sealed class Subscription<THandler> : BackgroundService
    where THandler : IMessageHandler
{
    private static readonly TimeSpan SetupRetryDelay = TimeSpan.FromSeconds(5);

    private readonly IBus bus;
    private readonly IServiceScopeFactory scopeFactory;
    private readonly SubscriptionOptions options;
    private readonly ILogger<Subscription<THandler>> logger;
    private CancellationToken stoppingToken;

    public Subscription(IBus bus, IServiceScopeFactory scopeFactory, SubscriptionOptions options,
        ILogger<Subscription<THandler>> logger)
    {
        ArgumentNullException.ThrowIfNull(bus);
        ArgumentNullException.ThrowIfNull(scopeFactory);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentException.ThrowIfNullOrWhiteSpace(options.Queue);
        ArgumentOutOfRangeException.ThrowIfZero(options.RoutingKeys.Count);

        this.bus = bus;
        this.scopeFactory = scopeFactory;
        this.options = options;
        this.logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        this.stoppingToken = stoppingToken;

        // The broker may not be up yet when the service starts (a fresh
        // docker-compose, a rolling restart), and a subscription that gave up
        // would leave the service running but deaf. Retry until it works.
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await DeclareTopologyAndConsumeAsync(stoppingToken).ConfigureAwait(false);
                return;
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Could not subscribe queue {Queue}, retrying in {Delay}", options.Queue, SetupRetryDelay);
                await DelayAsync(stoppingToken).ConfigureAwait(false);
            }
        }
    }

    private async Task DeclareTopologyAndConsumeAsync(CancellationToken cancellationToken)
    {
        var advanced = bus.Advanced;
        var dlxName = options.Queue + ".dlx";
        var dlqName = options.Queue + ".dlq";

        // Same declaration as RabbitMqMessageBus (durable topic), or the broker
        // rejects the redeclaration. Whoever starts first creates it.
        var events = await advanced
            .ExchangeDeclareAsync(ExchangeNames.Events, c => c.AsDurable(true).WithType(ExchangeType.Topic), cancellationToken)
            .ConfigureAwait(false);

        var dlx = await advanced
            .ExchangeDeclareAsync(dlxName, c => c.AsDurable(true).WithType(ExchangeType.Direct), cancellationToken)
            .ConfigureAwait(false);
        var dlq = await advanced
            .QueueDeclareAsync(dlqName, c => c.AsDurable(true).WithQueueType(QueueType.Quorum), cancellationToken)
            .ConfigureAwait(false);
        await advanced.BindAsync(dlx, dlq, dlqName, cancellationToken).ConfigureAwait(false);

        var queue = await advanced
            .QueueDeclareAsync(options.Queue, c => c
                .AsDurable(true)
                .WithQueueType(QueueType.Quorum)
                .WithDeadLetterExchange(dlx)
                .WithDeadLetterRoutingKey(dlqName)
                .WithArgument("x-delivery-limit", (long)options.DeliveryLimit), cancellationToken)
            .ConfigureAwait(false);

        foreach (var routingKey in options.RoutingKeys)
        {
            await advanced.BindAsync(events, queue, routingKey, cancellationToken).ConfigureAwait(false);
        }

        // Kept alive by the bus for the process's lifetime; not disposed here
        // because the service only ends when the host shuts down.
        _ = await advanced
            .ConsumeAsync(queue, OnMessageAsync, c => c.WithPrefetchCount(options.PrefetchCount))
            .ConfigureAwait(false);

        logger.LogInformation("Subscribed queue {Queue} to {Exchange} for {RoutingKeys}",
            options.Queue, ExchangeNames.Events, options.RoutingKeys);
    }

    internal async Task<AckStrategyAsync> OnMessageAsync(ReadOnlyMemory<byte> body, MessageProperties properties, MessageReceivedInfo info)
    {
        try
        {
            using var scope = scopeFactory.CreateScope();
            var handler = scope.ServiceProvider.GetRequiredService<THandler>();

            var outcome = await handler.HandleAsync(info.RoutingKey, body, stoppingToken).ConfigureAwait(false);

            return ToAckStrategy(outcome);
        }
        catch (Exception ex)
        {
            // An unexpected failure is treated as transient: the broker's
            // delivery limit is the backstop for one that never clears.
            logger.LogWarning(ex, "Handling a {RoutingKey} message on queue {Queue} failed, requeueing", info.RoutingKey, options.Queue);
            return AckStrategies.NackWithRequeueAsync;
        }
    }

    internal static AckStrategyAsync ToAckStrategy(MessageOutcome outcome) => outcome switch
    {
        MessageOutcome.Acknowledge => AckStrategies.AckAsync,
        MessageOutcome.Retry => AckStrategies.NackWithRequeueAsync,
        MessageOutcome.Reject => AckStrategies.NackWithoutRequeueAsync,
        _ => throw new ArgumentOutOfRangeException(nameof(outcome), outcome, "Unknown message outcome.")
    };

    private static async Task DelayAsync(CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(SetupRetryDelay, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // Shutting down; the loop condition ends the subscription.
        }
    }
}
