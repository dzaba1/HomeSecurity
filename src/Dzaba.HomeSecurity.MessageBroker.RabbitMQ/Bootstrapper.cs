using Microsoft.Extensions.DependencyInjection;

namespace Dzaba.HomeSecurity.MessageBroker.RabbitMQ;

public static class Bootstrapper
{
    public static IServiceCollection AddRabbitMQMessageBroker(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        return services;
    }
}
