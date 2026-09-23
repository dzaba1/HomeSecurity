using Dzaba.HomeSecurity.Devices.Service.Data.Entities;
using Dzaba.HomeSecurity.Domain;
using Microsoft.EntityFrameworkCore;

namespace Dzaba.HomeSecurity.Devices.Service.Data;

/// <summary>
/// Devices.Service's own database, not shared with Org.Service - see
/// docs/decisions/0016-devices-service-owns-its-own-database.md. Every
/// entity's global query filter is bound to the tenant resolved by
/// <see cref="ITenantContext"/> at construction time, never a
/// client-supplied value. TenantId is a plain column with no foreign key to
/// Organization (which lives in Org.Service's database): tenant validity is
/// enforced at the API edge instead.
/// </summary>
internal sealed class DevicesDbContext : DbContext
{
    private readonly Guid _tenantId;

    public DevicesDbContext(DbContextOptions<DevicesDbContext> options, ITenantContext tenantContext)
        : base(options)
    {
        ArgumentNullException.ThrowIfNull(tenantContext);

        _tenantId = tenantContext.TenantId;
    }

    public DbSet<Router> Routers => Set<Router>();

    public DbSet<Device> Devices => Set<Device>();

    public DbSet<AgentRouterBinding> AgentRouterBindings => Set<AgentRouterBinding>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Router>(e =>
        {
            e.HasKey(r => r.Id);
            e.HasIndex(r => r.TenantId);
            e.HasQueryFilter(r => r.TenantId == _tenantId);
        });

        modelBuilder.Entity<Device>(e =>
        {
            e.HasKey(d => d.Id);
            e.HasIndex(d => new { d.TenantId, d.MacAddress }).IsUnique();
            e.HasOne<Router>().WithMany().HasForeignKey(d => d.LastSeenViaRouterId).OnDelete(DeleteBehavior.SetNull);
            e.HasQueryFilter(d => d.TenantId == _tenantId);
        });

        modelBuilder.Entity<AgentRouterBinding>(e =>
        {
            e.HasKey(b => b.Id);
            // One router per agent credential; a router can have many
            // credentials (redundant agents) - see 15-router-credentials.md.
            e.HasIndex(b => new { b.TenantId, b.DeviceCredentialId }).IsUnique();
            e.HasOne<Router>().WithMany().HasForeignKey(b => b.RouterId);
            e.HasQueryFilter(b => b.TenantId == _tenantId);
        });
    }
}
