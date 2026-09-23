using EasyNetQ;
using EasyNetQ.Persistent;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Dzaba.HomeSecurity.MessageBroker.RabbitMQ;

/// <summary>
/// Reports healthy only once the producer connection is actually up -
/// EasyNetQ connects lazily/reconnects in the background, so this doesn't
/// just check that DI resolved an IBus instance.
/// </summary>
internal sealed class RabbitMqHealthCheck : IHealthCheck
{
    private readonly IBus bus;

    public RabbitMqHealthCheck(IBus bus)
    {
        ArgumentNullException.ThrowIfNull(bus);

        this.bus = bus;
    }

    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        var status = bus.Advanced.GetConnectionStatus(PersistentConnectionType.Producer);
        var result = status.State == PersistentConnectionState.Connected
            ? HealthCheckResult.Healthy()
            : HealthCheckResult.Unhealthy($"RabbitMQ producer connection state: {status.State}.");

        return Task.FromResult(result);
    }
}
