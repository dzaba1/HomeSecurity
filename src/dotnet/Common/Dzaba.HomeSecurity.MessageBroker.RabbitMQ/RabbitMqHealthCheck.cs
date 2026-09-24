using EasyNetQ;
using EasyNetQ.Persistent;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;

namespace Dzaba.HomeSecurity.MessageBroker.RabbitMQ;

/// <summary>
/// Reports healthy only once the producer connection is actually up. EasyNetQ
/// opens that connection lazily - on the first publish - so a service that
/// only publishes on demand would sit at "not initialised" forever and never
/// become ready; the check therefore asks for the connection itself, bounded
/// by a timeout, instead of just reading the current state.
/// </summary>
internal sealed class RabbitMqHealthCheck : IHealthCheck
{
    internal static readonly TimeSpan ConnectTimeout = TimeSpan.FromSeconds(5);

    private readonly IBus bus;
    private readonly ILogger<RabbitMqHealthCheck> logger;

    public RabbitMqHealthCheck(IBus bus, ILogger<RabbitMqHealthCheck> logger)
    {
        ArgumentNullException.ThrowIfNull(bus);
        ArgumentNullException.ThrowIfNull(logger);

        this.bus = bus;
        this.logger = logger;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(ConnectTimeout);

        var stateBefore = bus.Advanced.GetConnectionStatus(PersistentConnectionType.Producer).State;
        logger.LogDebug("Checking RabbitMQ producer connection, current state {State}", stateBefore);

        try
        {
            await bus.Advanced.EnsureConnectedAsync(PersistentConnectionType.Producer, timeout.Token).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning(ex, "RabbitMQ producer connection could not be established (state was {State})", stateBefore);
            return HealthCheckResult.Unhealthy($"RabbitMQ producer connection could not be established: {ex.Message}", ex);
        }

        var state = bus.Advanced.GetConnectionStatus(PersistentConnectionType.Producer).State;
        if (state != PersistentConnectionState.Connected)
        {
            logger.LogWarning("RabbitMQ producer connection is {State} after ensuring it", state);
            return HealthCheckResult.Unhealthy($"RabbitMQ producer connection state: {state}.");
        }

        return HealthCheckResult.Healthy();
    }
}
