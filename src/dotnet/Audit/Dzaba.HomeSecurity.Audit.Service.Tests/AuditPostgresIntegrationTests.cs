using Dzaba.HomeSecurity.Audit.Service.Data;
using Dzaba.HomeSecurity.Audit.Service.Data.Entities;
using Dzaba.HomeSecurity.Audit.Service.Ingestion;
using Dzaba.HomeSecurity.Domain;
using Dzaba.HomeSecurity.DomainEvents;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;

namespace Dzaba.HomeSecurity.Audit.Service.Tests;

/// <summary>
/// What only a real database can show: writers racing onto one hash chain, one
/// event delivered to two instances at once, a hash surviving a round trip
/// through Postgres's timestamp and json handling, and the append-only grants
/// the migration creates. The InMemory provider used elsewhere has no
/// transactions, so none of this can be tested there.
/// </summary>
/// <remarks>
/// Opt-in: set <c>AUDIT_TEST_POSTGRES</c> to a connection string for a Postgres
/// superuser (e.g. <c>Host=localhost;Port=5433;Username=postgres;Password=...</c>)
/// and each test gets a throwaway database, migrated with the service's real
/// migration, and dropped afterwards. Skipped when it is unset, so the normal
/// test run and CI need no database.
/// </remarks>
[TestFixture]
[Category("Postgres")]
public sealed class AuditPostgresIntegrationTests
{
    private const string ConnectionStringVariable = "AUDIT_TEST_POSTGRES";

    private string? adminConnectionString;
    private string? databaseName;
    private string connectionString = null!;

    [SetUp]
    public async Task CreateMigratedDatabase()
    {
        adminConnectionString = Environment.GetEnvironmentVariable(ConnectionStringVariable);
        if (string.IsNullOrWhiteSpace(adminConnectionString))
        {
            Assert.Ignore($"Set {ConnectionStringVariable} to a Postgres superuser connection string to run these.");
        }

        databaseName = "audit_test_" + Guid.NewGuid().ToString("N");
        await ExecuteAsync(adminConnectionString, $"CREATE DATABASE \"{databaseName}\"");
        connectionString = new NpgsqlConnectionStringBuilder(adminConnectionString) { Database = databaseName }.ConnectionString;

        using var db = NewContext();
        await db.Database.MigrateAsync();
    }

    [TearDown]
    public async Task DropDatabase()
    {
        if (adminConnectionString is null || databaseName is null)
        {
            return;
        }

        NpgsqlConnection.ClearAllPools();
        await ExecuteAsync(adminConnectionString, $"DROP DATABASE IF EXISTS \"{databaseName}\" WITH (FORCE)");
    }

    private static async Task ExecuteAsync(string connection, string sql)
    {
        await using var conn = new NpgsqlConnection(connection);
        await conn.OpenAsync();
        await using var command = new NpgsqlCommand(sql, conn);
        await command.ExecuteNonQueryAsync();
    }

    private AuditDbContext NewContext()
    {
        var options = new DbContextOptionsBuilder<AuditDbContext>()
            .UseNpgsql(connectionString, b => b.MigrationsAssembly(typeof(AuditDbContext).Assembly.GetName().Name))
            .Options;
        return new AuditDbContext(options, new StaticTenantContext(Guid.Empty));
    }

    private async Task<IngestResult> IngestAsync(DomainEventMessage message)
    {
        // A fresh context per message, exactly as the subscription gives each one.
        using var db = NewContext();
        return await new AuditEventIngestor(db, NullLogger<AuditEventIngestor>.Instance).IngestAsync(message, CancellationToken.None);
    }

    private async Task<List<AuditEvent>> StoredEventsAsync()
    {
        using var db = NewContext();
        return await db.AuditEvents.IgnoreQueryFilters().OrderBy(e => e.Seq).ToListAsync();
    }

    // Walk a chain in storage order: each event links to the one before it, each
    // hash is what its own content says it should be, and no two events claim
    // the same predecessor (which is what a fork looks like).
    private static void AssertChainIsIntact(IReadOnlyList<AuditEvent> chain)
    {
        chain.Should().NotBeEmpty();
        chain[0].PrevHash.Should().BeEmpty("the first event has no predecessor");
        for (var i = 0; i < chain.Count; i++)
        {
            chain[i].Hash.Should().Equal(AuditEventHasher.Compute(chain[i]), $"event {i}'s hash is its own content's hash");
            if (i > 0)
            {
                chain[i].PrevHash.Should().Equal(chain[i - 1].Hash, $"event {i} chains onto event {i - 1}");
            }
        }

        chain.Select(e => Convert.ToHexString(e.PrevHash)).Should().OnlyHaveUniqueItems("a fork would give two events one predecessor");
    }

    [Test]
    public async Task Ingest_WhenManyEventsRaceOntoTheSameFreshChain_ThenNoneIsLostAndTheChainNeverForks()
    {
        var tenantId = Guid.NewGuid();
        const int count = 25;

        var results = await Task.WhenAll(Enumerable.Range(0, count)
            .Select(_ => Task.Run(() => IngestAsync(TestEvents.Message(tenantId: tenantId, action: "role.assigned")))));

        results.Should().OnlyContain(r => r == IngestResult.Stored);
        var events = await StoredEventsAsync();
        events.Should().HaveCount(count);
        AssertChainIsIntact(events);
    }

    [Test]
    public async Task Ingest_WhenManyEventsRaceOntoAnExistingChain_ThenNoneIsLostAndTheChainNeverForks()
    {
        var tenantId = Guid.NewGuid();
        await IngestAsync(TestEvents.Message(tenantId: tenantId));
        const int count = 25;

        var results = await Task.WhenAll(Enumerable.Range(0, count)
            .Select(_ => Task.Run(() => IngestAsync(TestEvents.Message(tenantId: tenantId)))));

        results.Should().OnlyContain(r => r == IngestResult.Stored);
        var events = await StoredEventsAsync();
        events.Should().HaveCount(count + 1);
        AssertChainIsIntact(events);
    }

    [Test]
    public async Task Ingest_WhenSeveralTenantsAreIngestedConcurrently_ThenEachTenantsChainIsIntact()
    {
        var tenants = Enumerable.Range(0, 6).Select(_ => Guid.NewGuid()).ToArray();

        await Task.WhenAll(tenants.SelectMany(tenantId => Enumerable.Range(0, 6)
            .Select(_ => Task.Run(() => IngestAsync(TestEvents.Message(tenantId: tenantId))))));

        var events = await StoredEventsAsync();
        events.Should().HaveCount(tenants.Length * 6);
        foreach (var tenantId in tenants)
        {
            AssertChainIsIntact(events.Where(e => e.TenantId == tenantId).ToList());
        }
    }

    [Test]
    public async Task Ingest_WhenTheSameEventIsDeliveredToSeveralInstancesAtOnce_ThenItIsStoredExactlyOnce()
    {
        var message = TestEvents.Message(tenantId: Guid.NewGuid());

        var results = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => Task.Run(() => IngestAsync(message))));

        results.Count(r => r == IngestResult.Stored).Should().Be(1);
        results.Count(r => r == IngestResult.Duplicate).Should().Be(7);
        var events = await StoredEventsAsync();
        events.Should().ContainSingle();
        AssertChainIsIntact(events);
    }

    [Test]
    public async Task Ingest_WhenTheSameNewActorAppearsInSeveralEventsAtOnce_ThenThereIsOneToken()
    {
        var tenantId = Guid.NewGuid();

        await Task.WhenAll(Enumerable.Range(0, 8)
            .Select(_ => Task.Run(() => IngestAsync(TestEvents.Message(tenantId: tenantId, actorId: "brand-new-user")))));

        (await StoredEventsAsync()).Select(e => e.ActorRef).Distinct().Should().HaveCount(1);
        using var db = NewContext();
        (await db.ActorPseudonyms.CountAsync()).Should().Be(1);
    }

    [Test]
    public async Task Ingest_WhenTheEventIsReadBackFromPostgres_ThenItsHashStillVerifies()
    {
        // Two things that would silently break a stored hash: Postgres rounds a
        // 100 ns timestamp to a microsecond, and jsonb would reorder keys. The
        // ingestor truncates the time and the column is json, so neither happens.
        var precise = new DateTimeOffset(2026, 9, 26, 10, 0, 0, TimeSpan.Zero).AddTicks(1234567);
        await IngestAsync(TestEvents.Message(
            tenantId: Guid.NewGuid(), occurredAt: precise, metadata: """{"zebra": 1,   "apple":[1, 2], "mango":{"b":true,"a":null}}"""));

        var stored = (await StoredEventsAsync()).Single();

        stored.OccurredAt.Should().Be(AuditEventHasher.TruncateToMicroseconds(precise));
        stored.Metadata.Should().Be("""{"zebra":1,"apple":[1,2],"mango":{"b":true,"a":null}}""", "keys keep their order");
        stored.Hash.Should().Equal(AuditEventHasher.Compute(stored));
    }

    [Test]
    public async Task WriterRole_WhenItTriesToChangeOrRemoveAnEvent_ThenPostgresRefuses()
    {
        await IngestAsync(TestEvents.Message(tenantId: Guid.NewGuid()));

        await AssertPermissionDeniedAsync("audit_writer", "UPDATE \"AuditEvents\" SET \"Action\" = 'tampered'");
        await AssertPermissionDeniedAsync("audit_writer", "DELETE FROM \"AuditEvents\"");
        await AssertPermissionDeniedAsync("audit_writer", "TRUNCATE \"AuditEvents\"");
        await AssertPermissionDeniedAsync("audit_writer", "DELETE FROM \"ActorPseudonyms\"");
    }

    [Test]
    public async Task WriterRole_WhenItReadsInsertsAndMovesAChainHead_ThenPostgresAllowsIt()
    {
        await IngestAsync(TestEvents.Message(tenantId: Guid.NewGuid()));

        await AssertAllowedAsync("audit_writer", "SELECT count(*) FROM \"AuditEvents\"");
        await AssertAllowedAsync("audit_writer", "UPDATE \"ChainHeads\" SET \"LastHash\" = \"LastHash\"");
    }

    [Test]
    public async Task PurgerRole_WhenItDeletesOrInsertsOrUpdates_ThenOnlyDeletingIsAllowed()
    {
        await IngestAsync(TestEvents.Message(tenantId: Guid.NewGuid()));

        await AssertPermissionDeniedAsync("audit_purger", "UPDATE \"AuditEvents\" SET \"Action\" = 'tampered'");
        await AssertPermissionDeniedAsync("audit_purger", "INSERT INTO \"ChainHeads\" (\"TenantId\", \"Category\", \"LastHash\") VALUES (gen_random_uuid(), 'Access', '\\x00')");
        await AssertAllowedAsync("audit_purger", "DELETE FROM \"AuditEvents\"");
        await AssertAllowedAsync("audit_purger", "DELETE FROM \"ActorPseudonyms\"");
    }

    private async Task AssertPermissionDeniedAsync(string role, string sql)
    {
        var act = () => ExecuteAsRoleAsync(role, sql);

        (await act.Should().ThrowAsync<PostgresException>($"{role} must not be able to run: {sql}"))
            .Which.SqlState.Should().Be(PostgresErrorCodes.InsufficientPrivilege);
    }

    private async Task AssertAllowedAsync(string role, string sql)
    {
        var act = () => ExecuteAsRoleAsync(role, sql);

        await act.Should().NotThrowAsync($"{role} is meant to be able to run: {sql}");
    }

    // The test connects as the superuser and assumes the role: the privileges
    // that then apply are exactly the role's, which is what a login that is a
    // member of it would get.
    private async Task ExecuteAsRoleAsync(string role, string sql)
    {
        await using var conn = new NpgsqlConnection(connectionString);
        await conn.OpenAsync();
        await using var command = new NpgsqlCommand($"SET ROLE {role}; {sql}", conn);
        await command.ExecuteNonQueryAsync();
    }
}
