using Dzaba.HomeSecurity.MessageBroker.Contracts;
using EasyNetQ;
using Microsoft.Extensions.DependencyInjection;

namespace Dzaba.HomeSecurity.MessageBroker.RabbitMQ;

public static class Bootstrapper
{
    public static IServiceCollection AddRabbitMQMessageBroker(this IServiceCollection services, Func<IServiceProvider, ConnectionConfiguration> factory)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(factory);

        services.AddEasyNetQ(factory)
            .UseSystemTextJson();

        services.AddTransient<IEventPublisher, RabbitMQEventPublisher>();

        return services;
    }
}
