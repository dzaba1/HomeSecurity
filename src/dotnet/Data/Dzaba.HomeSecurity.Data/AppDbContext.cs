using Dzaba.HomeSecurity.Data.Entities;
using Dzaba.HomeSecurity.Domain;
using Microsoft.EntityFrameworkCore;

namespace Dzaba.HomeSecurity.Data;

/// <summary>
/// Shared across the Org and DeviceAuth services - both write to the same
/// physical Postgres database, so one migration history avoids two services
/// racing on schema changes. Every tenant-owned entity's global query
/// filter is bound to the tenant resolved by <see cref="ITenantContext"/> at
/// construction time - never a client-supplied value, per
/// docs/architecture/02-multi-tenancy.md.
/// </summary>
public sealed class AppDbContext : DbContext
{
    private readonly Guid _tenantId;

    public AppDbContext(DbContextOptions<AppDbContext> options, ITenantContext tenantContext)
        : base(options)
    {
        _tenantId = tenantContext.TenantId;
    }

    public DbSet<Organization> Organizations => Set<Organization>();

    public DbSet<Membership> Memberships => Set<Membership>();

    public DbSet<Permission> Permissions => Set<Permission>();

    public DbSet<Role> Roles => Set<Role>();

    public DbSet<RolePermission> RolePermissions => Set<RolePermission>();

    public DbSet<UserRole> UserRoles => Set<UserRole>();

    public DbSet<DeviceCredential> DeviceCredentials => Set<DeviceCredential>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Organization>(e =>
        {
            e.HasKey(o => o.Id);
            e.HasIndex(o => o.Identifier).IsUnique();
        });

        modelBuilder.Entity<Membership>(e =>
        {
            e.HasKey(m => new { m.OrganizationId, m.UserId });
            e.HasOne<Organization>().WithMany().HasForeignKey(m => m.OrganizationId);
            e.HasQueryFilter(m => m.OrganizationId == _tenantId);
        });

        modelBuilder.Entity<Permission>(e =>
        {
            e.HasKey(p => p.Key);

            e.HasData(
                new Permission { Key = PermissionKeys.DeviceCredentialView, Description = "View agent device credentials" },
                new Permission { Key = PermissionKeys.DeviceCredentialDelete, Description = "Delete agent device credentials" },
                new Permission { Key = PermissionKeys.LogsView, Description = "View logs" },
                new Permission { Key = PermissionKeys.OrgManageMembers, Description = "Manage organization members" },
                new Permission { Key = PermissionKeys.RouterView, Description = "View router metadata (never the secret)" },
                new Permission { Key = PermissionKeys.RouterManage, Description = "Create, update, and delete routers" },
                new Permission { Key = PermissionKeys.DeviceView, Description = "View network devices" },
                new Permission { Key = PermissionKeys.DeviceManage, Description = "Rename/acknowledge network devices" });
        });

        modelBuilder.Entity<Role>(e =>
        {
            e.HasKey(r => r.Id);
            e.HasOne<Organization>().WithMany().HasForeignKey(r => r.TenantId);
            // Null TenantId = system-default role, visible to every tenant.
            e.HasQueryFilter(r => r.TenantId == null || r.TenantId == _tenantId);

            e.HasData(
                new Role { Id = SystemRoles.OwnerId, TenantId = null, Name = SystemRoles.OwnerName },
                new Role { Id = SystemRoles.AdminId, TenantId = null, Name = SystemRoles.AdminName },
                new Role { Id = SystemRoles.MemberId, TenantId = null, Name = SystemRoles.MemberName },
                new Role { Id = SystemRoles.ViewerId, TenantId = null, Name = SystemRoles.ViewerName });
        });

        modelBuilder.Entity<RolePermission>(e =>
        {
            e.HasKey(rp => new { rp.RoleId, rp.PermissionKey });
            e.HasOne(rp => rp.Role).WithMany(r => r.RolePermissions).HasForeignKey(rp => rp.RoleId);
            e.HasOne(rp => rp.Permission).WithMany().HasForeignKey(rp => rp.PermissionKey);
            // No TenantId column of its own - isolation flows through Role.
            e.HasQueryFilter(rp => rp.Role.TenantId == null || rp.Role.TenantId == _tenantId);

            // Anonymous objects (FK scalars only) - Role/Permission are
            // non-null navigations, and HasData seed instances must not
            // carry navigation state.
            e.HasData(
                new { RoleId = SystemRoles.OwnerId, PermissionKey = PermissionKeys.DeviceCredentialView },
                new { RoleId = SystemRoles.OwnerId, PermissionKey = PermissionKeys.DeviceCredentialDelete },
                new { RoleId = SystemRoles.OwnerId, PermissionKey = PermissionKeys.LogsView },
                new { RoleId = SystemRoles.OwnerId, PermissionKey = PermissionKeys.OrgManageMembers },
                new { RoleId = SystemRoles.OwnerId, PermissionKey = PermissionKeys.RouterView },
                new { RoleId = SystemRoles.OwnerId, PermissionKey = PermissionKeys.RouterManage },
                new { RoleId = SystemRoles.OwnerId, PermissionKey = PermissionKeys.DeviceView },
                new { RoleId = SystemRoles.OwnerId, PermissionKey = PermissionKeys.DeviceManage },
                new { RoleId = SystemRoles.AdminId, PermissionKey = PermissionKeys.DeviceCredentialView },
                new { RoleId = SystemRoles.AdminId, PermissionKey = PermissionKeys.DeviceCredentialDelete },
                new { RoleId = SystemRoles.AdminId, PermissionKey = PermissionKeys.LogsView },
                new { RoleId = SystemRoles.AdminId, PermissionKey = PermissionKeys.OrgManageMembers },
                new { RoleId = SystemRoles.AdminId, PermissionKey = PermissionKeys.RouterView },
                new { RoleId = SystemRoles.AdminId, PermissionKey = PermissionKeys.RouterManage },
                new { RoleId = SystemRoles.AdminId, PermissionKey = PermissionKeys.DeviceView },
                new { RoleId = SystemRoles.AdminId, PermissionKey = PermissionKeys.DeviceManage },
                new { RoleId = SystemRoles.MemberId, PermissionKey = PermissionKeys.DeviceCredentialView },
                new { RoleId = SystemRoles.MemberId, PermissionKey = PermissionKeys.LogsView },
                new { RoleId = SystemRoles.MemberId, PermissionKey = PermissionKeys.DeviceView },
                new { RoleId = SystemRoles.ViewerId, PermissionKey = PermissionKeys.DeviceCredentialView },
                new { RoleId = SystemRoles.ViewerId, PermissionKey = PermissionKeys.LogsView },
                new { RoleId = SystemRoles.ViewerId, PermissionKey = PermissionKeys.DeviceView });
        });

        modelBuilder.Entity<UserRole>(e =>
        {
            e.HasKey(ur => new { ur.UserId, ur.TenantId, ur.RoleId });
            e.HasOne<Organization>().WithMany().HasForeignKey(ur => ur.TenantId);
            e.HasOne(ur => ur.Role).WithMany().HasForeignKey(ur => ur.RoleId);
            e.HasQueryFilter(ur => ur.TenantId == _tenantId);
        });

        modelBuilder.Entity<DeviceCredential>(e =>
        {
            e.HasKey(d => d.Id);
            e.HasOne<Organization>().WithMany().HasForeignKey(d => d.TenantId);
            e.HasQueryFilter(d => d.TenantId == _tenantId);
        });
    }
}
