using Dzaba.HomeSecurity.Audit.Service.Data;
using Dzaba.HomeSecurity.Audit.Service.Data.Entities;
using Dzaba.HomeSecurity.Domain;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace Dzaba.HomeSecurity.Audit.Service.Tests;

/// <summary>
/// Tenant-leak tests (docs/architecture/02-multi-tenancy.md): a context bound
/// to tenant B must never see an event tenant A's trail holds, even though both
/// share the same InMemory store.
/// </summary>
[TestFixture]
public sealed class AuditDbContextTests
{
    private string databaseName = null!;

    [SetUp]
    public void SetUp()
    {
        databaseName = Guid.NewGuid().ToString();
    }

    private AuditDbContext CreateContext(Guid tenantId)
    {
        var options = new DbContextOptionsBuilder<AuditDbContext>().UseInMemoryDatabase(databaseName).Options;
        return new AuditDbContext(options, new StaticTenantContext(tenantId));
    }

    private static AuditEvent NewEvent(Guid? tenantId) => new()
    {
        EventId = Guid.NewGuid(),
        OccurredAt = DateTimeOffset.UtcNow,
        ReceivedAt = DateTimeOffset.UtcNow,
        TenantId = tenantId,
        Category = AuditCategory.Access,
        ActorType = AuditActorType.User,
        ActorRef = Guid.NewGuid(),
        Action = "role.assigned",
        TargetType = "UserRole",
        TargetId = "user:role",
        Metadata = "{}",
        PrevHash = [],
        Hash = [1, 2, 3],
    };

    [Test]
    public async Task AuditEvents_WhenQueriedFromAnotherTenant_ThenTheyAreHidden()
    {
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        using (var context = CreateContext(tenantA))
        {
            context.AuditEvents.Add(NewEvent(tenantA));
            await context.SaveChangesAsync();
        }

        using (var context = CreateContext(tenantA))
        {
            (await context.AuditEvents.CountAsync()).Should().Be(1);
        }

        using (var context = CreateContext(tenantB))
        {
            (await context.AuditEvents.CountAsync()).Should().Be(0);
        }
    }

    [Test]
    public async Task AuditEvents_WhenTwoTenantsHaveEvents_ThenEachSeesOnlyItsOwn()
    {
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        var eventA = NewEvent(tenantA);
        var eventB = NewEvent(tenantB);
        using (var context = CreateContext(Guid.Empty))
        {
            // Ingestion writes for every tenant; filters never apply to inserts.
            context.AuditEvents.AddRange(eventA, eventB);
            await context.SaveChangesAsync();
        }

        using (var context = CreateContext(tenantA))
        {
            (await context.AuditEvents.Select(e => e.EventId).ToListAsync()).Should().BeEquivalentTo([eventA.EventId]);
        }

        using (var context = CreateContext(tenantB))
        {
            (await context.AuditEvents.Select(e => e.EventId).ToListAsync()).Should().BeEquivalentTo([eventB.EventId]);
        }
    }

    [Test]
    public async Task AuditEvents_WhenTheEventHasNoTenant_ThenNoTenantAndNoTenantlessContextSeesIt()
    {
        // A pre-tenant event (a login) must not leak to a tenant, nor to a
        // request that failed to resolve one (Guid.Empty).
        using (var context = CreateContext(Guid.Empty))
        {
            context.AuditEvents.Add(NewEvent(null));
            await context.SaveChangesAsync();
        }

        foreach (var tenantId in new[] { Guid.NewGuid(), Guid.Empty })
        {
            using var context = CreateContext(tenantId);
            (await context.AuditEvents.CountAsync()).Should().Be(0);
        }
    }

    [Test]
    public async Task ChainHeads_WhenQueriedFromAnyTenant_ThenTheyAreVisible()
    {
        // Chain heads carry no personal or tenant-facing data and are only read
        // by the ingestion path, which serves every tenant: not filtered by design.
        using (var context = CreateContext(Guid.NewGuid()))
        {
            context.ChainHeads.Add(new ChainHead { TenantId = Guid.NewGuid(), Category = AuditCategory.Router, LastSeq = 1, LastHash = [1] });
            await context.SaveChangesAsync();
        }

        using (var context = CreateContext(Guid.NewGuid()))
        {
            (await context.ChainHeads.CountAsync()).Should().Be(1);
        }
    }

    [Test]
    public async Task ChainHeads_WhenSameTenantHasTwoCategories_ThenEachHasItsOwnChain()
    {
        var tenantId = Guid.NewGuid();
        using (var context = CreateContext(tenantId))
        {
            context.ChainHeads.AddRange(
                new ChainHead { TenantId = tenantId, Category = AuditCategory.Access, LastSeq = 1, LastHash = [1] },
                new ChainHead { TenantId = tenantId, Category = AuditCategory.Router, LastSeq = 2, LastHash = [2] });
            await context.SaveChangesAsync();
        }

        using (var context = CreateContext(tenantId))
        {
            (await context.ChainHeads.CountAsync()).Should().Be(2);
        }
    }
}
