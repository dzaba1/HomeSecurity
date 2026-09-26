using Dzaba.HomeSecurity.MessageBroker.Contracts;
using EasyNetQ;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

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

    /// <summary>
    /// Subscribes the service to the events exchange: declares its queue (with
    /// dead-letter exchange and queue), binds the routing keys, and hands each
    /// message to <typeparamref name="THandler"/>, resolved in a fresh scope per
    /// message so the handler can use scoped services such as a DbContext. Needs
    /// <see cref="AddDzabaHomeSecurityRabbitMqMessageBus"/> to have been called
    /// (it registers the broker connection).
    /// </summary>
    public static IServiceCollection AddDzabaHomeSecurityRabbitMqSubscription<THandler>(this IServiceCollection services, SubscriptionOptions options)
        where THandler : class, IMessageHandler
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(options);

        services.AddTransient<THandler>();
        services.AddHostedService(sp => new Subscription<THandler>(
            sp.GetRequiredService<IBus>(),
            sp.GetRequiredService<IServiceScopeFactory>(),
            options,
            sp.GetRequiredService<ILogger<Subscription<THandler>>>()));

        return services;
    }
}
