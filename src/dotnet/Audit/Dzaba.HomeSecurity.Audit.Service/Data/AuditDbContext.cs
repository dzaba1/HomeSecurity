using Dzaba.HomeSecurity.Audit.Service.Data.Entities;
using Dzaba.HomeSecurity.Domain;
using Microsoft.EntityFrameworkCore;

namespace Dzaba.HomeSecurity.Audit.Service.Data;

/// <summary>
/// Audit.Service's own database, not shared with any other service - see
/// docs/decisions/0016-devices-service-owns-its-own-database.md. The audit
/// trail must survive, and stay readable, when the service whose action it
/// records is degraded, and nothing else should need write access to it.
/// </summary>
/// <remarks>
/// A tenant-facing read is bound to one tenant by the global query filter on
/// <see cref="AuditEvent"/>, exactly as in the other services; events with no
/// tenant (a login) match no tenant and are never returned to one. The
/// ingestion path, which writes for every tenant, inserts (filters never
/// apply to inserts) and reads chain heads, which have no filter. Deliberately
/// enforced by the database as well as by this model: see the grants in the
/// InitialCreate migration.
/// </remarks>
internal sealed class AuditDbContext : DbContext
{
    private readonly Guid _tenantId;

    public AuditDbContext(DbContextOptions<AuditDbContext> options, ITenantContext tenantContext)
        : base(options)
    {
        ArgumentNullException.ThrowIfNull(tenantContext);

        _tenantId = tenantContext.TenantId;
    }

    public DbSet<AuditEvent> AuditEvents => Set<AuditEvent>();

    public DbSet<ActorPseudonym> ActorPseudonyms => Set<ActorPseudonym>();

    public DbSet<ChainHead> ChainHeads => Set<ChainHead>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<AuditEvent>(e =>
        {
            e.HasKey(a => a.Seq);
            e.Property(a => a.Seq).ValueGeneratedOnAdd();

            e.HasIndex(a => a.EventId).IsUnique();

            // The tenant's timeline, newest first.
            e.HasIndex(a => new { a.TenantId, a.OccurredAt }).IsDescending(false, true);
            e.HasIndex(a => new { a.TenantId, a.Action });
            e.HasIndex(a => new { a.TenantId, a.ActorRef });
            e.HasIndex(a => new { a.TenantId, a.TargetType, a.TargetId });
            // Walking one chain in order, for integrity checks and purging.
            e.HasIndex(a => new { a.TenantId, a.Category, a.Seq });

            e.Property(a => a.Action).HasMaxLength(100);
            e.Property(a => a.TargetType).HasMaxLength(100);
            e.Property(a => a.TargetId).HasMaxLength(200);
            e.Property(a => a.Category).HasConversion<string>().HasMaxLength(30);
            e.Property(a => a.ActorType).HasConversion<string>().HasMaxLength(10);
            // json, not jsonb: keeps the text byte-for-byte, so the hash
            // computed over it still matches after a round trip.
            e.Property(a => a.Metadata).HasColumnType("json");

            e.HasQueryFilter(a => a.TenantId == _tenantId);
        });

        modelBuilder.Entity<ActorPseudonym>(e =>
        {
            e.HasKey(p => new { p.ActorType, p.ActorId });
            e.HasIndex(p => p.Token).IsUnique();

            e.Property(p => p.ActorType).HasConversion<string>().HasMaxLength(10);
            e.Property(p => p.ActorId).HasMaxLength(200);
        });

        modelBuilder.Entity<ChainHead>(e =>
        {
            e.HasKey(h => new { h.TenantId, h.Category });

            e.Property(h => h.Category).HasConversion<string>().HasMaxLength(30);
            // What serializes writers to one chain: the UPDATE is conditional on
            // the tip still being what the new event chained onto.
            e.Property(h => h.LastHash).IsConcurrencyToken();
        });
    }
}
