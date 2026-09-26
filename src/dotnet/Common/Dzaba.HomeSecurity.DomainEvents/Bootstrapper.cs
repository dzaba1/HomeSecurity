using Microsoft.Extensions.DependencyInjection;

namespace Dzaba.HomeSecurity.DomainEvents;

public static class Bootstrapper
{
    /// <summary>
    /// Registers <see cref="IDomainEventPublisher"/> and
    /// <see cref="ICurrentActor"/> for a service that publishes domain events.
    /// The service still has to register an <c>IMessageBus</c> and an
    /// <c>ITenantContext</c> (the latter scoped, as everywhere else).
    /// </summary>
    public static IServiceCollection AddDzabaHomeSecurityDomainEvents(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddHttpContextAccessor();
        services.AddTransient<ICurrentActor, HttpContextCurrentActor>();
        services.AddTransient<IDomainEventPublisher, DomainEventPublisher>();

        return services;
    }
}
