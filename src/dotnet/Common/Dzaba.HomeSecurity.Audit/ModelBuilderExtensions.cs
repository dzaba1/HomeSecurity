using Microsoft.EntityFrameworkCore;

namespace Dzaba.HomeSecurity.Audit;

public static class ModelBuilderExtensions
{
    /// <summary>
    /// Adds the audit outbox table to a service's own model. Call from the
    /// service DbContext's OnModelCreating, then regenerate that service's
    /// migration so the table exists.
    /// </summary>
    public static ModelBuilder ApplyAuditOutbox(this ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        modelBuilder.Entity<AuditOutboxEntry>(e =>
        {
            e.HasKey(o => o.Id);
            e.Property(o => o.Id).ValueGeneratedNever();
            e.Property(o => o.RoutingKey).IsRequired().HasMaxLength(150);
            e.Property(o => o.Payload).IsRequired();
            // The relay reads oldest-first.
            e.HasIndex(o => o.CreatedAt);
        });

        return modelBuilder;
    }
}
