using Dzaba.Org.Contracts;
using Finbuckle.MultiTenant.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Dzaba.Org;

internal class OrgDbContext : DbContext
{
    public OrgDbContext(DbContextOptions<OrgDbContext> options) : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        ConfigureMembership(modelBuilder.Entity<Membership>());
        ConfigureTenant(modelBuilder.Entity<TenantInfo>());
        ConfigureRole(modelBuilder.Entity<Role>());
        ConfigureRoleMemberships(modelBuilder.Entity<RoleMembership>());
    }

    private void ConfigureMembership(EntityTypeBuilder<Membership> builder)
    {
        builder.HasKey(e => new { e.TenantId, e.UserId });
        builder.Property(e => e.UserId).IsRequired();
        builder.Property(e => e.TenantId).IsRequired();

        builder.HasOne<GuidTenantInfo>()
            .WithMany()
            .HasForeignKey(e => e.TenantId)
            .HasPrincipalKey(e => e.GuidId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(e => e.UserId)
            .IsUnique(false)
            .HasDatabaseName("IX_Membership_UserId");
    }

    private void ConfigureTenant(EntityTypeBuilder<TenantInfo> builder)
    {
        builder.HasIndex(ti => ti.Identifier)
            .IsUnique()
            .HasDatabaseName("IX_Tenants_Identifier");
    }
    
    private void ConfigureRole(EntityTypeBuilder<Role> builder)
    {
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Name).IsRequired();
    }

    private void ConfigureRoleMemberships(EntityTypeBuilder<RoleMembership> builder)
    {
        builder.HasKey(e => new { e.RoleId, e.UserId});
        builder.HasOne<Role>()
            .WithMany()
            .HasForeignKey(e => e.RoleId)
            .HasPrincipalKey(e => e.Id)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(e => e.UserId)
            .IsUnique(false)
            .HasDatabaseName("IX_RoleMembership_UserId");
    }

    public DbSet<GuidTenantInfo> Tenants => Set<GuidTenantInfo>();

    public DbSet<Membership> Memberships => Set<Membership>();

    public DbSet<Role> Roles => Set<Role>();

    public DbSet<RoleMembership> RoleMemberships => Set<RoleMembership>();
}
