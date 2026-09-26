using System.Text.Json;
using Dzaba.HomeSecurity.Audit.Service.Data;
using Dzaba.HomeSecurity.Audit.Service.Data.Entities;
using Dzaba.HomeSecurity.DomainEvents;
using Microsoft.EntityFrameworkCore;

namespace Dzaba.HomeSecurity.Audit.Service.Ingestion;

internal enum IngestResult
{
    /// <summary>The event is now part of the audit trail.</summary>
    Stored,

    /// <summary>An event with that id was already stored (a redelivery); nothing was written.</summary>
    Duplicate
}

/// <summary>
/// Turns a received domain event into a stored, hash-chained audit event.
/// </summary>
/// <remarks>
/// Delivery is at-least-once and several instances of the service may consume
/// at once, so this is written to be safe to repeat and to race:
/// <list type="bullet">
/// <item>A redelivered event is dropped by its id (checked first for the common
/// case, and by the unique index for a true race between two instances).</item>
/// <item>An event chains onto the tip of its (tenant, category) chain, and moves
/// the tip in the same transaction. The tip's hash is a concurrency token, so if
/// another writer moved it first this attempt fails cleanly and re-reads the tip:
/// a chain never forks.</item>
/// </list>
/// See docs/architecture/16-auditing-and-compliance.md sections 3 and 4.
/// </remarks>
internal sealed class AuditEventIngestor
{
    // A chain is contended only by events for the same tenant and category
    // arriving at the same instant. Every round of contention has a winner, so
    // the worst case for one event is losing once to each of the others: this
    // bounds that at roughly a full prefetch window from several instances
    // (the default is 20 per instance). Past it the event is handed back to the
    // broker for another delivery rather than being lost.
    private const int MaxAttempts = 50;

    private const int MaxBackOffMilliseconds = 50;

    private readonly AuditDbContext db;
    private readonly ILogger<AuditEventIngestor> logger;

    public AuditEventIngestor(AuditDbContext db, ILogger<AuditEventIngestor> logger)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(logger);

        this.db = db;
        this.logger = logger;
    }

    public async Task<IngestResult> IngestAsync(DomainEventMessage message, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(message);

        // The context is bound to no tenant, so the tenant filter would hide
        // every event from this check; it has to look at all of them.
        if (await EventExistsAsync(message.EventId, cancellationToken).ConfigureAwait(false))
        {
            logger.LogDebug("Event {EventId} was already stored, dropping the duplicate", message.EventId);
            return IngestResult.Duplicate;
        }

        var actorType = ToActorType(message.Actor.Type);
        var actorRef = await GetOrCreateActorTokenAsync(actorType, message.Actor.Id, cancellationToken).ConfigureAwait(false);

        var category = AuditCategories.For(message.Action);
        var chainTenantId = message.TenantId ?? Guid.Empty;

        for (var attempt = 1; attempt <= MaxAttempts; attempt++)
        {
            // A failed attempt leaves its half-built entities tracked.
            db.ChangeTracker.Clear();

            var head = await db.ChainHeads
                .FirstOrDefaultAsync(h => h.TenantId == chainTenantId && h.Category == category, cancellationToken)
                .ConfigureAwait(false);

            var auditEvent = new AuditEvent
            {
                EventId = message.EventId,
                OccurredAt = AuditEventHasher.TruncateToMicroseconds(message.OccurredAt),
                ReceivedAt = AuditEventHasher.TruncateToMicroseconds(DateTimeOffset.UtcNow),
                TenantId = message.TenantId,
                Category = category,
                ActorType = actorType,
                ActorRef = actorRef,
                Action = message.Action,
                TargetType = message.Target.Type,
                TargetId = message.Target.Id,
                // The exact JSON text, hashed as stored: see AuditEvent.Metadata.
                Metadata = JsonSerializer.Serialize(message.Metadata),
                PrevHash = head?.LastHash ?? [],
                Hash = [],
            };
            auditEvent.Hash = AuditEventHasher.Compute(auditEvent);

            db.AuditEvents.Add(auditEvent);
            if (head is null)
            {
                db.ChainHeads.Add(new ChainHead { TenantId = chainTenantId, Category = category, LastHash = auditEvent.Hash });
            }
            else
            {
                head.LastHash = auditEvent.Hash;
            }

            try
            {
                await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                return IngestResult.Stored;
            }
            catch (DbUpdateConcurrencyException)
            {
                // Another writer moved this chain's tip between our read and
                // our write: chain onto the new tip instead.
                logger.LogDebug("Chain ({TenantId}, {Category}) moved under event {EventId}, retrying (attempt {Attempt})",
                    chainTenantId, category, message.EventId, attempt);
                await BackOffAsync(attempt, cancellationToken).ConfigureAwait(false);
            }
            // DbUpdateException is what a relational provider (Npgsql) throws for
            // a unique-key violation; ArgumentException ("An item with the same
            // key has already been added") is what EF Core's InMemory provider
            // throws for the same case - a quirk of that provider, not wrapped
            // as DbUpdateException. Both are handled, narrowly, around this one
            // SaveChanges: the realistic causes are the unique EventId (another
            // instance stored this event meanwhile) and two writers creating a
            // chain's first head at once. Telling them apart by "does the event
            // exist now?" is sound because SaveChanges is one transaction on a
            // relational database: an event that exists was committed whole,
            // by someone else. (The InMemory provider has no transactions and
            // can't model this; the Postgres integration tests cover it.)
            catch (Exception ex) when (ex is DbUpdateException or ArgumentException)
            {
                db.ChangeTracker.Clear();
                if (await EventExistsAsync(message.EventId, cancellationToken).ConfigureAwait(false))
                {
                    logger.LogDebug("Event {EventId} was stored by another instance meanwhile", message.EventId);
                    return IngestResult.Duplicate;
                }

                logger.LogDebug(ex, "Storing event {EventId} hit a key conflict on chain ({TenantId}, {Category}), retrying (attempt {Attempt})",
                    message.EventId, chainTenantId, category, attempt);
                await BackOffAsync(attempt, cancellationToken).ConfigureAwait(false);
            }
        }

        throw new InvalidOperationException(
            $"Could not chain event {message.EventId} onto ({chainTenantId}, {category}) after {MaxAttempts} attempts.");
    }

    // A short random pause, growing with each lost round up to a cap, so
    // writers that just collided don't collide again in lockstep.
    private static Task BackOffAsync(int attempt, CancellationToken cancellationToken) =>
        Task.Delay(Random.Shared.Next(1, Math.Min(5 * attempt, MaxBackOffMilliseconds) + 1), cancellationToken);

    private Task<bool> EventExistsAsync(Guid eventId, CancellationToken cancellationToken) =>
        db.AuditEvents.IgnoreQueryFilters().AnyAsync(e => e.EventId == eventId, cancellationToken);

    private async Task<Guid> GetOrCreateActorTokenAsync(AuditActorType actorType, string actorId, CancellationToken cancellationToken)
    {
        var existing = await FindTokenAsync(actorType, actorId, cancellationToken).ConfigureAwait(false);
        if (existing is not null)
        {
            return existing.Value;
        }

        var pseudonym = new ActorPseudonym
        {
            ActorType = actorType,
            ActorId = actorId,
            Token = Guid.NewGuid(),
            CreatedAt = DateTimeOffset.UtcNow,
        };
        db.ActorPseudonyms.Add(pseudonym);

        try
        {
            await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return pseudonym.Token;
        }
        // See IngestAsync for why both exception types: two instances meeting the
        // same actor for the first time both try to create the row, one loses.
        catch (Exception ex) when (ex is DbUpdateException or ArgumentException)
        {
            db.ChangeTracker.Clear();

            var winner = await FindTokenAsync(actorType, actorId, cancellationToken).ConfigureAwait(false);
            return winner ?? throw new InvalidOperationException(
                $"Could not create or find the pseudonym for {actorType} '{actorId}'.", ex);
        }
    }

    private async Task<Guid?> FindTokenAsync(AuditActorType actorType, string actorId, CancellationToken cancellationToken)
    {
        var token = await db.ActorPseudonyms
            .Where(p => p.ActorType == actorType && p.ActorId == actorId)
            .Select(p => (Guid?)p.Token)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        return token;
    }

    private static AuditActorType ToActorType(ActorType actorType) => actorType switch
    {
        ActorType.User => AuditActorType.User,
        ActorType.Device => AuditActorType.Device,
        ActorType.System => AuditActorType.System,
        _ => throw new ArgumentOutOfRangeException(nameof(actorType), actorType, "Unknown actor type."),
    };
}
