using Dzaba.HomeSecurity.Audit.Service.Data;
using Dzaba.HomeSecurity.Audit.Service.Data.Entities;
using Dzaba.HomeSecurity.Audit.Service.Ingestion;
using Dzaba.HomeSecurity.Domain;
using Dzaba.HomeSecurity.DomainEvents;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace Dzaba.HomeSecurity.Audit.Service.Tests;

[TestFixture]
public sealed class AuditEventIngestorTests
{
    private string databaseName = null!;

    [SetUp]
    public void SetUp()
    {
        databaseName = Guid.NewGuid().ToString();
    }

    // The ingestion path is bound to no tenant (it writes for all of them), and
    // each message gets its own context, as the subscription gives it.
    private AuditDbContext NewContext()
    {
        var options = new DbContextOptionsBuilder<AuditDbContext>().UseInMemoryDatabase(databaseName).Options;
        return new AuditDbContext(options, new StaticTenantContext(Guid.Empty));
    }

    private async Task<IngestResult> IngestAsync(DomainEventMessage message)
    {
        using var db = NewContext();
        return await new AuditEventIngestor(db, NullLogger<AuditEventIngestor>.Instance).IngestAsync(message, CancellationToken.None);
    }

    private async Task<List<AuditEvent>> StoredEventsAsync()
    {
        using var db = NewContext();
        return await db.AuditEvents.IgnoreQueryFilters().OrderBy(e => e.Seq).ToListAsync();
    }

    [Test]
    public async Task Ingest_WhenTheEventIsNew_ThenItIsStoredWithItsCategoryAndAValidHash()
    {
        var tenantId = Guid.NewGuid();
        var message = TestEvents.Message(tenantId: tenantId, action: "role.assigned", actorId: "keycloak-user-1");

        var result = await IngestAsync(message);

        result.Should().Be(IngestResult.Stored);
        var stored = (await StoredEventsAsync()).Should().ContainSingle().Subject;
        stored.EventId.Should().Be(message.EventId);
        stored.TenantId.Should().Be(tenantId);
        stored.Category.Should().Be(AuditCategory.Access);
        stored.Action.Should().Be("role.assigned");
        stored.TargetType.Should().Be("UserRole");
        stored.TargetId.Should().Be("user-2:role-1");
        stored.PrevHash.Should().BeEmpty("it is the first event in its chain");
        stored.Hash.Should().Equal(AuditEventHasher.Compute(stored));
        stored.ReceivedAt.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromMinutes(1));
    }

    [Test]
    public async Task Ingest_WhenTheEventCarriesMetadata_ThenItIsStoredAsTheJsonTextItArrivedAs()
    {
        await IngestAsync(TestEvents.Message(metadata: """{"roleName":"Admin","permissionKeys":["a","b"]}"""));

        (await StoredEventsAsync()).Single().Metadata.Should().Be("""{"roleName":"Admin","permissionKeys":["a","b"]}""");
    }

    [Test]
    public async Task Ingest_WhenTheTimestampHasSubMicrosecondPrecision_ThenItIsTruncatedSoTheHashSurvivesTheDatabase()
    {
        var precise = new DateTimeOffset(2026, 9, 26, 10, 0, 0, TimeSpan.Zero).AddTicks(1234567);

        await IngestAsync(TestEvents.Message(occurredAt: precise));

        var stored = (await StoredEventsAsync()).Single();
        stored.OccurredAt.Should().Be(AuditEventHasher.TruncateToMicroseconds(precise));
        stored.Hash.Should().Equal(AuditEventHasher.Compute(stored));
    }

    [Test]
    public async Task Ingest_WhenTheActorIsSeen_ThenTheEventHoldsAnOpaqueTokenNotTheRealId()
    {
        await IngestAsync(TestEvents.Message(actorId: "keycloak-user-1"));

        var stored = (await StoredEventsAsync()).Single();
        using var db = NewContext();
        var pseudonym = await db.ActorPseudonyms.SingleAsync();
        pseudonym.ActorId.Should().Be("keycloak-user-1");
        pseudonym.ActorType.Should().Be(AuditActorType.User);
        stored.ActorRef.Should().Be(pseudonym.Token);
        stored.ActorType.Should().Be(AuditActorType.User);
    }

    [Test]
    public async Task Ingest_WhenTheSameActorActsAgain_ThenTheSameTokenIsUsed()
    {
        await IngestAsync(TestEvents.Message(actorId: "user-1"));
        await IngestAsync(TestEvents.Message(actorId: "user-1"));

        (await StoredEventsAsync()).Select(e => e.ActorRef).Distinct().Should().HaveCount(1);
        using var db = NewContext();
        (await db.ActorPseudonyms.CountAsync()).Should().Be(1);
    }

    [Test]
    public async Task Ingest_WhenDifferentActorsAct_ThenEachGetsItsOwnToken()
    {
        await IngestAsync(TestEvents.Message(actorId: "user-1"));
        await IngestAsync(TestEvents.Message(actorId: "user-2"));

        (await StoredEventsAsync()).Select(e => e.ActorRef).Distinct().Should().HaveCount(2);
    }

    [Test]
    public async Task Ingest_WhenAUserAndADeviceShareAnId_ThenTheyAreDifferentActors()
    {
        // A Keycloak subject and an agent credential id live in different id
        // spaces; the same string must not merge them.
        await IngestAsync(TestEvents.Message(actorType: "User", actorId: "same-id"));
        await IngestAsync(TestEvents.Message(actorType: "Device", actorId: "same-id"));

        (await StoredEventsAsync()).Select(e => e.ActorRef).Distinct().Should().HaveCount(2);
    }

    [Test]
    public async Task Ingest_WhenTheEventWasAlreadyStored_ThenItIsADuplicateAndNothingIsWritten()
    {
        var message = TestEvents.Message();
        await IngestAsync(message);

        var second = await IngestAsync(message);

        second.Should().Be(IngestResult.Duplicate);
        (await StoredEventsAsync()).Should().HaveCount(1);
        using var db = NewContext();
        (await db.ChainHeads.CountAsync()).Should().Be(1);
    }

    [Test]
    public async Task Ingest_WhenASecondEventFollowsInTheSameChain_ThenItChainsOntoTheFirst()
    {
        var tenantId = Guid.NewGuid();
        await IngestAsync(TestEvents.Message(tenantId: tenantId, action: "role.assigned"));
        await IngestAsync(TestEvents.Message(tenantId: tenantId, action: "membership.added"));

        var events = await StoredEventsAsync();
        events[1].PrevHash.Should().Equal(events[0].Hash);
        events[1].Hash.Should().Equal(AuditEventHasher.Compute(events[1]));
        using var db = NewContext();
        (await db.ChainHeads.SingleAsync()).LastHash.Should().Equal(events[1].Hash);
    }

    [Test]
    public async Task Ingest_WhenTwoTenantsHaveEvents_ThenEachTenantHasItsOwnChain()
    {
        await IngestAsync(TestEvents.Message(tenantId: Guid.NewGuid()));
        await IngestAsync(TestEvents.Message(tenantId: Guid.NewGuid()));

        var events = await StoredEventsAsync();
        events.Should().OnlyContain(e => e.PrevHash.Length == 0, "each is the first in its own tenant's chain");
    }

    [Test]
    public async Task Ingest_WhenOneTenantHasTwoCategories_ThenEachCategoryHasItsOwnChain()
    {
        var tenantId = Guid.NewGuid();
        await IngestAsync(TestEvents.Message(tenantId: tenantId, action: "role.assigned"));
        await IngestAsync(TestEvents.Message(tenantId: tenantId, action: "router.created"));

        var events = await StoredEventsAsync();
        events.Select(e => e.Category).Should().BeEquivalentTo([AuditCategory.Access, AuditCategory.Router]);
        events.Should().OnlyContain(e => e.PrevHash.Length == 0);
    }

    [Test]
    public async Task Ingest_WhenTheEventHasNoTenant_ThenItIsStoredWithNoTenantOnItsOwnChain()
    {
        await IngestAsync(TestEvents.Message(tenantId: null, action: "identity.login.succeeded"));
        await IngestAsync(TestEvents.Message(tenantId: null, action: "identity.login.failed"));

        var events = await StoredEventsAsync();
        events.Should().OnlyContain(e => e.TenantId == null && e.Category == AuditCategory.Identity);
        events[1].PrevHash.Should().Equal(events[0].Hash);
        using var db = NewContext();
        (await db.ChainHeads.SingleAsync()).TenantId.Should().Be(Guid.Empty);
    }

    // Not here: what happens when writers race onto one chain, or one event is
    // delivered to two instances at once. That rests on SaveChanges being a real
    // transaction, which the InMemory provider used above does not model, so it
    // is tested against Postgres - see AuditPostgresIntegrationTests.
}
