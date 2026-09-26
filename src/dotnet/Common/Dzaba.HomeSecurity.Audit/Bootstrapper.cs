using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Dzaba.HomeSecurity.Audit;

public static class Bootstrapper
{
    /// <summary>
    /// Wires a service up to record audit events through its own DbContext:
    /// <see cref="IAuditRecorder"/> and <see cref="ICurrentActor"/> for the
    /// service code, and the background relay that publishes the outbox to
    /// RabbitMQ. The service still has to call
    /// <see cref="ModelBuilderExtensions.ApplyAuditOutbox"/> in its
    /// DbContext and register an <c>IMessageBus</c> and
    /// <c>ITenantContext</c> (the latter scoped, as everywhere else).
    /// </summary>
    public static IServiceCollection AddDzabaHomeSecurityAuditOutbox<TContext>(this IServiceCollection services, IConfiguration configuration)
        where TContext : DbContext
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.Configure<AuditOutboxOptions>(configuration.GetSection(AuditOutboxOptions.SectionName));

        services.AddHttpContextAccessor();
        services.AddTransient<ICurrentActor, HttpContextCurrentActor>();
        services.AddTransient<IAuditRecorder, AuditRecorder<TContext>>();
        services.AddHostedService<AuditOutboxRelay<TContext>>();
        services.AddHealthChecks().AddCheck<AuditOutboxHealthCheck<TContext>>("audit-outbox", tags: ["degraded"]);

        return services;
    }
}
