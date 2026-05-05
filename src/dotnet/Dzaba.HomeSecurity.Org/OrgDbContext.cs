using Dzaba.HomeSecurity.Org.Contracts;
using Finbuckle.MultiTenant.EntityFrameworkCore.Stores;
using Microsoft.EntityFrameworkCore;

namespace Dzaba.HomeSecurity.Org;

internal class OrgDbContext : EFCoreStoreDbContext<OrgTenantInfo>
{
    public OrgDbContext(DbContextOptions<OrgDbContext> options) : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Membership>(entity =>
        {
            entity.HasKey(e => new { e.TenantId, e.UserId });
            entity.Property(e => e.UserId).IsRequired();
            entity.Property(e => e.TenantId).IsRequired();
        });

        modelBuilder.Entity<OrgTenantInfo>(entity =>
        {
            entity.Property(e => e.Name).IsRequired();
        });
    }

    public DbSet<Membership> Memberships => Set<Membership>();
}
