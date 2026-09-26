using System.Text.Json;
using Dzaba.HomeSecurity.Audit.Contracts;
using Dzaba.HomeSecurity.MessageBroker.Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Dzaba.HomeSecurity.Audit;

/// <summary>
/// Publishes the audit events a service has recorded in its outbox table to
/// RabbitMQ, oldest first, deleting each row once the broker has accepted it.
/// Delivery is at-least-once: a crash between publish and delete republishes
/// the row, which Audit.Service drops by its unique event id.
/// </summary>
internal sealed class AuditOutboxRelay<TContext> : BackgroundService
    where TContext : DbContext
{
    private readonly IServiceScopeFactory scopeFactory;
    private readonly IOptions<AuditOutboxOptions> options;
    private readonly ILogger<AuditOutboxRelay<TContext>> logger;

    public AuditOutboxRelay(
        IServiceScopeFactory scopeFactory,
        IOptions<AuditOutboxOptions> options,
        ILogger<AuditOutboxRelay<TContext>> logger)
    {
        ArgumentNullException.ThrowIfNull(scopeFactory);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(logger);

        this.scopeFactory = scopeFactory;
        this.options = options;
        this.logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var settings = options.Value;

        while (!stoppingToken.IsCancellationRequested)
        {
            var publishedFullBatch = false;
            try
            {
                var published = await DrainOnceAsync(stoppingToken).ConfigureAwait(false);
                publishedFullBatch = published >= settings.BatchSize;
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                // The broker or database being down must not kill the relay:
                // the rows are still in the outbox, so it retries next pass.
                logger.LogWarning(ex, "Audit outbox relay pass failed, will retry");
            }

            if (!publishedFullBatch)
            {
                await DelayAsync(settings.PollInterval, stoppingToken).ConfigureAwait(false);
            }
        }
    }

    internal async Task<int> DrainOnceAsync(CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<TContext>();
        var bus = scope.ServiceProvider.GetRequiredService<IMessageBus>();

        var entries = await context.Set<AuditOutboxEntry>()
            .OrderBy(o => o.CreatedAt)
            .ThenBy(o => o.Id)
            .Take(options.Value.BatchSize)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var published = 0;
        foreach (var entry in entries)
        {
            var message = Deserialize(entry);
            if (message is null)
            {
                continue;
            }

            await bus.PublishAsync(entry.RoutingKey, message, cancellationToken).ConfigureAwait(false);

            context.Set<AuditOutboxEntry>().Remove(entry);
            try
            {
                await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (DbUpdateConcurrencyException)
            {
                // Another instance of this service already published and
                // deleted the row; nothing left to do for it.
                logger.LogDebug("Audit outbox row {EventId} was already relayed by another instance", entry.Id);
                context.ChangeTracker.Clear();
            }

            published++;
        }

        if (published > 0)
        {
            logger.LogDebug("Relayed {Count} audit events", published);
        }

        return published;
    }

    private AuditEventMessage? Deserialize(AuditOutboxEntry entry)
    {
        try
        {
            return JsonSerializer.Deserialize<AuditEventMessage>(entry.Payload);
        }
        catch (JsonException ex)
        {
            // Never deleted: dropping an audit record silently is exactly what
            // the outbox exists to prevent. It stays in the outbox, showing up
            // in the backlog health check, until someone repairs it.
            logger.LogError(ex, "Audit outbox row {EventId} has an unreadable payload and is being skipped", entry.Id);
            return null;
        }
    }

    private static async Task DelayAsync(TimeSpan delay, CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // Shutting down; the loop condition ends the relay.
        }
    }
}
