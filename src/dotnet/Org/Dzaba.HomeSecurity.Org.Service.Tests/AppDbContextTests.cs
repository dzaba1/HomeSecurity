using Dzaba.HomeSecurity.Org.Service.Data;
using Dzaba.HomeSecurity.Org.Service.Data.Entities;
using Dzaba.HomeSecurity.Domain;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NUnit.Framework;

namespace Dzaba.HomeSecurity.Org.Service.Tests;

[TestFixture]
public class AppDbContextTests : DataTestFixture
{
    [Test]
    public async Task EnsureCreatedAsync_WhenCalled_ThenModelBuildsAndAllSetsAreQueryable()
    {
        // This is the extent to which "the migration" can be exercised against
        // the InMemory provider: InMemory has no concept of applying migration
        // SQL, but EnsureCreatedAsync builds the store from the exact same
        // OnModelCreating configuration the migration was generated from, so a
        // broken model (bad keys/relationships) fails here the same way it
        // would fail `dotnet ef migrations add`. CreateContext already calls
        // EnsureCreated() (see DataTestFixture) so the seed data below is
        // populated - this second call is a no-op confirming that.
        using var context = CreateContext(Guid.NewGuid());

        var created = await context.Database.EnsureCreatedAsync();

        created.Should().BeFalse();
        (await context.Organizations.ToListAsync()).Should().BeEmpty();
        (await context.Memberships.ToListAsync()).Should().BeEmpty();
        (await context.Permissions.ToListAsync()).Should().HaveCount(8);
        (await context.Roles.ToListAsync()).Should().HaveCount(4);
        (await context.RolePermissions.ToListAsync()).Should().HaveCount(22);
        (await context.UserRoles.ToListAsync()).Should().BeEmpty();
        (await context.DeviceCredentials.ToListAsync()).Should().BeEmpty();
    }

    [Test]
    public async Task SaveChangesAsync_WhenFullGraphAdded_ThenItRoundTripsWithinTheSameTenant()
    {
        var tenantId = Guid.NewGuid();

        using (var context = CreateContext(tenantId))
        {
            context.Organizations.Add(new Organization { Id = tenantId, Identifier = "acme", Name = "Acme" });

            var role = new Role { Id = Guid.NewGuid(), TenantId = tenantId, Name = "Custom" };
            context.Roles.Add(role);
            context.RolePermissions.Add(new RolePermission { RoleId = role.Id, PermissionKey = PermissionKeys.DeviceCredentialView });
            context.UserRoles.Add(new UserRole { UserId = "user-1", TenantId = tenantId, RoleId = role.Id });
            context.Memberships.Add(new Membership { OrganizationId = tenantId, UserId = "user-1" });
            context.DeviceCredentials.Add(new DeviceCredential
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                Name = "Laptop",
                SecretHash = "hash",
                SecretCreatedAt = DateTimeOffset.UtcNow,
                Status = DeviceCredentialStatus.Active
            });

            await context.SaveChangesAsync();
        }

        using (var context = CreateContext(tenantId))
        {
            (await context.Organizations.FindAsync(tenantId)).Should().NotBeNull();
            (await context.Permissions.FindAsync("device_credential.view")).Should().NotBeNull();
            (await context.Roles.CountAsync()).Should().Be(5); // 4 system + 1 custom
            (await context.RolePermissions.CountAsync()).Should().Be(23); // 22 system + 1 custom
            (await context.UserRoles.CountAsync()).Should().Be(1);
            (await context.Memberships.CountAsync()).Should().Be(1);
            (await context.DeviceCredentials.CountAsync()).Should().Be(1);
        }
    }

    [Test]
    public async Task Roles_WhenQueriedFromAnotherTenant_ThenCustomRoleIsHiddenButSystemRolesAreVisible()
    {
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();

        using (var context = CreateContext(tenantA))
        {
            context.Roles.Add(new Role { Id = Guid.NewGuid(), TenantId = tenantA, Name = "Custom-A" });
            await context.SaveChangesAsync();
        }

        using (var context = CreateContext(tenantA))
        {
            var names = (await context.Roles.Select(r => r.Name).ToListAsync());
            names.Should().BeEquivalentTo("Custom-A", "Owner", "Admin", "Member", "Viewer");
        }

        using (var context = CreateContext(tenantB))
        {
            var names = (await context.Roles.Select(r => r.Name).ToListAsync());
            names.Should().BeEquivalentTo("Owner", "Admin", "Member", "Viewer");
        }
    }

    [Test]
    public async Task RolePermissions_WhenQueriedFromAnotherTenant_ThenRowsAreHiddenViaTheRoleNavigation()
    {
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        var roleId = Guid.NewGuid();

        using (var context = CreateContext(tenantA))
        {
            context.Roles.Add(new Role { Id = roleId, TenantId = tenantA, Name = "Custom-A" });
            context.RolePermissions.Add(new RolePermission { RoleId = roleId, PermissionKey = PermissionKeys.DeviceCredentialDelete });
            await context.SaveChangesAsync();
        }

        using (var context = CreateContext(tenantA))
        {
            (await context.RolePermissions.CountAsync()).Should().Be(23); // 22 system + 1 custom
        }

        using (var context = CreateContext(tenantB))
        {
            (await context.RolePermissions.CountAsync()).Should().Be(22); // system only
        }
    }

    [Test]
    public async Task UserRoles_WhenSameUserHoldsRolesInTwoTenants_ThenEachTenantOnlySeesItsOwnAssignment()
    {
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        const string userId = "shared-user";

        using (var context = CreateContext(tenantA))
        {
            var roleA = new Role { Id = Guid.NewGuid(), TenantId = tenantA, Name = "Admin-A" };
            context.Roles.Add(roleA);
            context.UserRoles.Add(new UserRole { UserId = userId, TenantId = tenantA, RoleId = roleA.Id });
            await context.SaveChangesAsync();
        }

        using (var context = CreateContext(tenantB))
        {
            var roleB = new Role { Id = Guid.NewGuid(), TenantId = tenantB, Name = "Viewer-B" };
            context.Roles.Add(roleB);
            context.UserRoles.Add(new UserRole { UserId = userId, TenantId = tenantB, RoleId = roleB.Id });
            await context.SaveChangesAsync();
        }

        using (var context = CreateContext(tenantA))
        {
            var roleNames = await context.UserRoles
                .Where(ur => ur.UserId == userId)
                .Select(ur => ur.Role.Name)
                .ToListAsync();
            roleNames.Should().BeEquivalentTo("Admin-A");
        }

        using (var context = CreateContext(tenantB))
        {
            var roleNames = await context.UserRoles
                .Where(ur => ur.UserId == userId)
                .Select(ur => ur.Role.Name)
                .ToListAsync();
            roleNames.Should().BeEquivalentTo("Viewer-B");
        }
    }

    [Test]
    public async Task DeviceCredentials_WhenQueriedFromAnotherTenant_ThenTheyAreHidden()
    {
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();

        using (var context = CreateContext(tenantA))
        {
            context.DeviceCredentials.Add(new DeviceCredential
            {
                Id = Guid.NewGuid(),
                TenantId = tenantA,
                Name = "Phone",
                SecretHash = "hash",
                SecretCreatedAt = DateTimeOffset.UtcNow,
                Status = DeviceCredentialStatus.Active
            });
            await context.SaveChangesAsync();
        }

        using (var context = CreateContext(tenantA))
        {
            (await context.DeviceCredentials.CountAsync()).Should().Be(1);
        }

        using (var context = CreateContext(tenantB))
        {
            (await context.DeviceCredentials.CountAsync()).Should().Be(0);
        }
    }

    [Test]
    public async Task Memberships_WhenQueriedFromAnotherTenant_ThenTheyAreHidden()
    {
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();

        using (var context = CreateContext(tenantA))
        {
            context.Memberships.Add(new Membership { OrganizationId = tenantA, UserId = "user-1" });
            await context.SaveChangesAsync();
        }

        using (var context = CreateContext(tenantA))
        {
            (await context.Memberships.CountAsync()).Should().Be(1);
        }

        using (var context = CreateContext(tenantB))
        {
            (await context.Memberships.CountAsync()).Should().Be(0);
        }
    }

    [Test]
    public async Task Permissions_WhenQueried_ThenTheFixedCatalogIsSeeded()
    {
        using var context = CreateContext(Guid.NewGuid());

        var keys = await context.Permissions.Select(p => p.Key).ToListAsync();

        keys.Should().BeEquivalentTo(
            PermissionKeys.DeviceCredentialView,
            PermissionKeys.DeviceCredentialDelete,
            PermissionKeys.LogsView,
            PermissionKeys.OrgManageMembers,
            PermissionKeys.RouterView,
            PermissionKeys.RouterManage,
            PermissionKeys.DeviceView,
            PermissionKeys.DeviceManage);
    }

    [Test]
    public async Task Roles_WhenQueried_ThenTheFourSystemDefaultRolesAreSeededWithFixedIds()
    {
        using var context = CreateContext(Guid.NewGuid());

        var roles = await context.Roles.ToListAsync();

        roles.Should().OnlyContain(r => r.TenantId == null);
        roles.Select(r => r.Id).Should().BeEquivalentTo(
            new[] { SystemRoles.OwnerId, SystemRoles.AdminId, SystemRoles.MemberId, SystemRoles.ViewerId });
        roles.Select(r => r.Name).Should().BeEquivalentTo(
            SystemRoles.OwnerName, SystemRoles.AdminName, SystemRoles.MemberName, SystemRoles.ViewerName);
    }

    [Test]
    public async Task RolePermissions_WhenQueried_ThenOwnerGrantsAllEightPermissions()
    {
        using var context = CreateContext(Guid.NewGuid());

        var keys = await context.RolePermissions
            .Where(rp => rp.RoleId == SystemRoles.OwnerId)
            .Select(rp => rp.PermissionKey)
            .ToListAsync();

        keys.Should().BeEquivalentTo(
            PermissionKeys.DeviceCredentialView,
            PermissionKeys.DeviceCredentialDelete,
            PermissionKeys.LogsView,
            PermissionKeys.OrgManageMembers,
            PermissionKeys.RouterView,
            PermissionKeys.RouterManage,
            PermissionKeys.DeviceView,
            PermissionKeys.DeviceManage);
    }

    [Test]
    public async Task RolePermissions_WhenQueried_ThenAdminGrantsAllEightPermissions()
    {
        using var context = CreateContext(Guid.NewGuid());

        var keys = await context.RolePermissions
            .Where(rp => rp.RoleId == SystemRoles.AdminId)
            .Select(rp => rp.PermissionKey)
            .ToListAsync();

        keys.Should().BeEquivalentTo(
            PermissionKeys.DeviceCredentialView,
            PermissionKeys.DeviceCredentialDelete,
            PermissionKeys.LogsView,
            PermissionKeys.OrgManageMembers,
            PermissionKeys.RouterView,
            PermissionKeys.RouterManage,
            PermissionKeys.DeviceView,
            PermissionKeys.DeviceManage);
    }

    [Test]
    public async Task RolePermissions_WhenQueried_ThenMemberGrantsDeviceCredentialViewLogsViewAndDeviceViewOnly()
    {
        using var context = CreateContext(Guid.NewGuid());

        var keys = await context.RolePermissions
            .Where(rp => rp.RoleId == SystemRoles.MemberId)
            .Select(rp => rp.PermissionKey)
            .ToListAsync();

        keys.Should().BeEquivalentTo(
            PermissionKeys.DeviceCredentialView,
            PermissionKeys.LogsView,
            PermissionKeys.DeviceView);
    }

    [Test]
    public async Task RolePermissions_WhenQueried_ThenViewerGrantsDeviceCredentialViewLogsViewAndDeviceViewOnly()
    {
        using var context = CreateContext(Guid.NewGuid());

        var keys = await context.RolePermissions
            .Where(rp => rp.RoleId == SystemRoles.ViewerId)
            .Select(rp => rp.PermissionKey)
            .ToListAsync();

        keys.Should().BeEquivalentTo(
            PermissionKeys.DeviceCredentialView,
            PermissionKeys.LogsView,
            PermissionKeys.DeviceView);
    }
}
