using Dzaba.HomeSecurity.MessageBroker.Contracts;
using EasyNetQ;
using Microsoft.Extensions.DependencyInjection;

namespace Dzaba.HomeSecurity.MessageBroker.RabbitMQ;

/// <summary>
/// The only place EasyNetQ-specific wiring happens - callers depend on
/// IMessageBus (from Dzaba.HomeSecurity.MessageBroker.Contracts) and this
/// extension method, never on EasyNetQ types directly.
/// </summary>
public static class Bootstrapper
{
    public static IServiceCollection AddDzabaHomeSecurityRabbitMqMessageBus(this IServiceCollection services, string connectionString)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);

        services.AddEasyNetQ(connectionString);
        services.AddTransient<IMessageBus, RabbitMqMessageBus>();
        services.AddHealthChecks().AddCheck<RabbitMqHealthCheck>("rabbitmq");

        return services;
    }
}
