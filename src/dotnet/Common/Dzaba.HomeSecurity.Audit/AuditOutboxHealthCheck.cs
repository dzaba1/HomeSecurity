using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace Dzaba.HomeSecurity.Audit;

/// <summary>
/// Reports Degraded when the oldest event in the outbox has been waiting
/// longer than <see cref="AuditOutboxOptions.MaxBacklogAge"/> - the relay is
/// stalled or the broker is unreachable. Registered with the "degraded" tag,
/// so it never takes the service out of rotation (audit events keep queueing
/// safely in the database while the service does its real work).
/// </summary>
internal sealed class AuditOutboxHealthCheck<TContext> : IHealthCheck
    where TContext : DbContext
{
    private readonly IServiceScopeFactory scopeFactory;
    private readonly IOptions<AuditOutboxOptions> options;

    public AuditOutboxHealthCheck(IServiceScopeFactory scopeFactory, IOptions<AuditOutboxOptions> options)
    {
        ArgumentNullException.ThrowIfNull(scopeFactory);
        ArgumentNullException.ThrowIfNull(options);

        this.scopeFactory = scopeFactory;
        this.options = options;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        using var scope = scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<TContext>();

        var oldest = await dbContext.Set<AuditOutboxEntry>()
            .OrderBy(o => o.CreatedAt)
            .Select(o => (DateTimeOffset?)o.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        if (oldest is null)
        {
            return HealthCheckResult.Healthy("Audit outbox is empty");
        }

        var age = DateTimeOffset.UtcNow - oldest.Value;
        return age > options.Value.MaxBacklogAge
            ? HealthCheckResult.Degraded($"Oldest unpublished audit event is {age:c} old")
            : HealthCheckResult.Healthy($"Oldest unpublished audit event is {age:c} old");
    }
}
