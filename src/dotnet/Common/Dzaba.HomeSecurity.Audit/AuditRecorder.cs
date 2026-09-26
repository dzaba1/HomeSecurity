using System.Text.Json;
using Dzaba.HomeSecurity.Audit.Contracts;
using Dzaba.HomeSecurity.Domain;
using Microsoft.EntityFrameworkCore;

namespace Dzaba.HomeSecurity.Audit;

internal sealed class AuditRecorder<TContext> : IAuditRecorder
    where TContext : DbContext
{
    private readonly TContext context;
    private readonly ICurrentActor currentActor;
    private readonly ITenantContext tenantContext;

    public AuditRecorder(TContext context, ICurrentActor currentActor, ITenantContext tenantContext)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(currentActor);
        ArgumentNullException.ThrowIfNull(tenantContext);

        this.context = context;
        this.currentActor = currentActor;
        this.tenantContext = tenantContext;
    }

    public Guid Record(
        string action,
        string targetType,
        string targetId,
        IReadOnlyDictionary<string, object?>? metadata = null,
        Guid? tenantId = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(action);
        ArgumentException.ThrowIfNullOrWhiteSpace(targetType);
        ArgumentException.ThrowIfNullOrWhiteSpace(targetId);

        var resolvedTenantId = tenantId ?? tenantContext.TenantId;
        var message = new AuditEventMessage
        {
            EventId = Guid.NewGuid(),
            OccurredAt = DateTimeOffset.UtcNow,
            // Guid.Empty is the tenant context's "no tenant" value (routes
            // without an orgId), which the envelope spells as null.
            TenantId = resolvedTenantId == Guid.Empty ? null : resolvedTenantId,
            Actor = currentActor.GetCurrent(),
            Action = action,
            Target = new Target { Type = targetType, Id = targetId },
            Metadata = metadata ?? new Dictionary<string, object?>()
        };

        context.Set<AuditOutboxEntry>().Add(new AuditOutboxEntry
        {
            Id = message.EventId,
            RoutingKey = AuditActions.RoutingKey(action),
            Payload = JsonSerializer.Serialize(message),
            CreatedAt = message.OccurredAt
        });

        return message.EventId;
    }
}
