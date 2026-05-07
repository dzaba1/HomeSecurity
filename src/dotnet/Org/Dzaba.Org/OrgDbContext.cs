using Dzaba.Org.Contracts;
using Finbuckle.MultiTenant.Abstractions;
using Finbuckle.MultiTenant.EntityFrameworkCore.Stores;
using Microsoft.EntityFrameworkCore;

namespace Dzaba.Org;

internal class OrgDbContext : EFCoreStoreDbContext<TenantInfo>
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

        modelBuilder.Entity<TenantInfo>(entity =>
        {
            entity.Property(e => e.Name).IsRequired();
        });
    }

    public DbSet<Membership> Memberships => Set<Membership>();
}
