using Dzaba.HomeSecurity.Data.Entities;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NUnit.Framework;

namespace Dzaba.HomeSecurity.Data.Tests;

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
        // would fail `dotnet ef migrations add`.
        using var context = CreateContext(Guid.NewGuid());

        var created = await context.Database.EnsureCreatedAsync();

        created.Should().BeTrue();
        (await context.Organizations.ToListAsync()).Should().BeEmpty();
        (await context.Memberships.ToListAsync()).Should().BeEmpty();
        (await context.Permissions.ToListAsync()).Should().BeEmpty();
        (await context.Roles.ToListAsync()).Should().BeEmpty();
        (await context.RolePermissions.ToListAsync()).Should().BeEmpty();
        (await context.UserRoles.ToListAsync()).Should().BeEmpty();
        (await context.Devices.ToListAsync()).Should().BeEmpty();
    }

    [Test]
    public async Task SaveChangesAsync_WhenFullGraphAdded_ThenItRoundTripsWithinTheSameTenant()
    {
        var tenantId = Guid.NewGuid();

        using (var context = CreateContext(tenantId))
        {
            context.Organizations.Add(new Organization { Id = tenantId, Identifier = "acme", Name = "Acme" });
            context.Permissions.Add(new Permission { Key = "device.view", Description = "View devices" });

            var role = new Role { Id = Guid.NewGuid(), TenantId = tenantId, Name = "Custom" };
            context.Roles.Add(role);
            context.RolePermissions.Add(new RolePermission { RoleId = role.Id, PermissionKey = "device.view" });
            context.UserRoles.Add(new UserRole { UserId = "user-1", TenantId = tenantId, RoleId = role.Id });
            context.Memberships.Add(new Membership { OrganizationId = tenantId, UserId = "user-1" });
            context.Devices.Add(new Device
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                Name = "Laptop",
                SecretHash = "hash",
                SecretCreatedAt = DateTimeOffset.UtcNow,
                Status = DeviceStatus.Active
            });

            await context.SaveChangesAsync();
        }

        using (var context = CreateContext(tenantId))
        {
            (await context.Organizations.FindAsync(tenantId)).Should().NotBeNull();
            (await context.Permissions.FindAsync("device.view")).Should().NotBeNull();
            (await context.Roles.CountAsync()).Should().Be(1);
            (await context.RolePermissions.CountAsync()).Should().Be(1);
            (await context.UserRoles.CountAsync()).Should().Be(1);
            (await context.Memberships.CountAsync()).Should().Be(1);
            (await context.Devices.CountAsync()).Should().Be(1);
        }
    }

    [Test]
    public async Task Roles_WhenQueriedFromAnotherTenant_ThenCustomRoleIsHiddenButSystemRoleIsVisible()
    {
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();

        using (var context = CreateContext(tenantA))
        {
            context.Roles.Add(new Role { Id = Guid.NewGuid(), TenantId = tenantA, Name = "Custom-A" });
            context.Roles.Add(new Role { Id = Guid.NewGuid(), TenantId = null, Name = "Owner" });
            await context.SaveChangesAsync();
        }

        using (var context = CreateContext(tenantA))
        {
            var names = (await context.Roles.Select(r => r.Name).ToListAsync());
            names.Should().BeEquivalentTo("Custom-A", "Owner");
        }

        using (var context = CreateContext(tenantB))
        {
            var names = (await context.Roles.Select(r => r.Name).ToListAsync());
            names.Should().BeEquivalentTo("Owner");
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
            context.Permissions.Add(new Permission { Key = "device.delete", Description = "Delete devices" });
            context.Roles.Add(new Role { Id = roleId, TenantId = tenantA, Name = "Custom-A" });
            context.RolePermissions.Add(new RolePermission { RoleId = roleId, PermissionKey = "device.delete" });
            await context.SaveChangesAsync();
        }

        using (var context = CreateContext(tenantA))
        {
            (await context.RolePermissions.CountAsync()).Should().Be(1);
        }

        using (var context = CreateContext(tenantB))
        {
            (await context.RolePermissions.CountAsync()).Should().Be(0);
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
    public async Task Devices_WhenQueriedFromAnotherTenant_ThenTheyAreHidden()
    {
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();

        using (var context = CreateContext(tenantA))
        {
            context.Devices.Add(new Device
            {
                Id = Guid.NewGuid(),
                TenantId = tenantA,
                Name = "Phone",
                SecretHash = "hash",
                SecretCreatedAt = DateTimeOffset.UtcNow,
                Status = DeviceStatus.Active
            });
            await context.SaveChangesAsync();
        }

        using (var context = CreateContext(tenantA))
        {
            (await context.Devices.CountAsync()).Should().Be(1);
        }

        using (var context = CreateContext(tenantB))
        {
            (await context.Devices.CountAsync()).Should().Be(0);
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
}
