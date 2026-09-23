using Dzaba.HomeSecurity.Devices.Service.Data;
using Dzaba.HomeSecurity.Devices.Service.Data.Entities;
using Dzaba.HomeSecurity.Domain;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace Dzaba.HomeSecurity.Devices.Service.Tests;

/// <summary>
/// Tenant-leak tests (docs/architecture/02-multi-tenancy.md): for every
/// tenant-owned entity, a context bound to tenant B must never see a row
/// tenant A wrote, even though both share the same InMemory store.
/// </summary>
[TestFixture]
public sealed class DevicesDbContextTests
{
    private string databaseName = null!;

    [SetUp]
    public void SetUp()
    {
        databaseName = Guid.NewGuid().ToString();
    }

    private DevicesDbContext CreateContext(Guid tenantId)
    {
        var options = new DbContextOptionsBuilder<DevicesDbContext>().UseInMemoryDatabase(databaseName).Options;
        return new DevicesDbContext(options, new StaticTenantContext(tenantId));
    }

    private static Router NewRouter(Guid tenantId) => new()
    {
        Id = Guid.NewGuid(),
        TenantId = tenantId,
        Name = "Living room router",
        Host = "192.168.1.1",
        Protocol = RouterProtocol.WebScrape,
        AuthMode = RouterAuthMode.HttpBasic,
        Username = "admin",
        EncryptedSecret = [1, 2, 3],
        UpdatedAt = DateTimeOffset.UtcNow,
    };

    [Test]
    public async Task Routers_WhenQueriedFromAnotherTenant_ThenTheyAreHidden()
    {
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();

        using (var context = CreateContext(tenantA))
        {
            context.Routers.Add(NewRouter(tenantA));
            await context.SaveChangesAsync();
        }

        using (var context = CreateContext(tenantA))
        {
            (await context.Routers.CountAsync()).Should().Be(1);
        }

        using (var context = CreateContext(tenantB))
        {
            (await context.Routers.CountAsync()).Should().Be(0);
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
                MacAddress = "AA:BB:CC:DD:EE:FF",
                Status = DeviceStatus.Unknown,
                FirstSeenAt = DateTimeOffset.UtcNow,
                LastSeenAt = DateTimeOffset.UtcNow,
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
    public async Task AgentRouterBindings_WhenQueriedFromAnotherTenant_ThenTheyAreHidden()
    {
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();

        using (var context = CreateContext(tenantA))
        {
            var router = NewRouter(tenantA);
            context.Routers.Add(router);
            context.AgentRouterBindings.Add(new AgentRouterBinding
            {
                Id = Guid.NewGuid(),
                TenantId = tenantA,
                DeviceCredentialId = Guid.NewGuid(),
                RouterId = router.Id,
                CreatedAt = DateTimeOffset.UtcNow,
            });
            await context.SaveChangesAsync();
        }

        using (var context = CreateContext(tenantA))
        {
            (await context.AgentRouterBindings.CountAsync()).Should().Be(1);
        }

        using (var context = CreateContext(tenantB))
        {
            (await context.AgentRouterBindings.CountAsync()).Should().Be(0);
        }
    }
}
