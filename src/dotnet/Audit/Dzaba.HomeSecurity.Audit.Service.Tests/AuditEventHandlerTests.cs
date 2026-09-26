using Dzaba.HomeSecurity.Audit.Service.Data;
using Dzaba.HomeSecurity.Audit.Service.Ingestion;
using Dzaba.HomeSecurity.Domain;
using Dzaba.HomeSecurity.MessageBroker.Contracts;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace Dzaba.HomeSecurity.Audit.Service.Tests;

/// <summary>
/// What the handler tells the broker to do with each kind of message: the
/// difference between "store it", "try again" and "park it in the dead-letter
/// queue" is what keeps a poison message from blocking the trail and a
/// transient outage from losing an event.
/// </summary>
[TestFixture]
public sealed class AuditEventHandlerTests
{
    private string databaseName = null!;

    [SetUp]
    public void SetUp()
    {
        databaseName = Guid.NewGuid().ToString();
    }

    private AuditDbContext NewContext()
    {
        var options = new DbContextOptionsBuilder<AuditDbContext>().UseInMemoryDatabase(databaseName).Options;
        return new AuditDbContext(options, new StaticTenantContext(Guid.Empty));
    }

    private static AuditEventHandler NewHandler(AuditDbContext db) =>
        new(new AuditEventIngestor(db, NullLogger<AuditEventIngestor>.Instance), NullLogger<AuditEventHandler>.Instance);

    private async Task<MessageOutcome> HandleAsync(string routingKey, byte[] body, CancellationToken cancellationToken = default)
    {
        using var db = NewContext();
        return await NewHandler(db).HandleAsync(routingKey, body, cancellationToken);
    }

    private async Task<int> StoredCountAsync()
    {
        using var db = NewContext();
        return await db.AuditEvents.IgnoreQueryFilters().CountAsync();
    }

    [Test]
    public async Task Handle_WhenTheMessageIsAWellFormedEvent_ThenItIsStoredAndAcknowledged()
    {
        var outcome = await HandleAsync("role.assigned", TestEvents.Body(TestEvents.Json(tenantId: Guid.NewGuid())));

        outcome.Should().Be(MessageOutcome.Acknowledge);
        (await StoredCountAsync()).Should().Be(1);
    }

    [Test]
    public async Task Handle_WhenTheSameEventIsDeliveredAgain_ThenItIsAcknowledgedAndStoredOnce()
    {
        // At-least-once delivery: a redelivery must be a quiet no-op, not an
        // error the broker then retries forever.
        var body = TestEvents.Body(TestEvents.Json(eventId: Guid.NewGuid(), tenantId: Guid.NewGuid()));

        var first = await HandleAsync("role.assigned", body);
        var second = await HandleAsync("role.assigned", body);

        first.Should().Be(MessageOutcome.Acknowledge);
        second.Should().Be(MessageOutcome.Acknowledge);
        (await StoredCountAsync()).Should().Be(1);
    }

    [Test]
    public async Task Handle_WhenTheEventHasNoTenant_ThenItIsStoredAndAcknowledged()
    {
        var outcome = await HandleAsync("identity.login.succeeded",
            TestEvents.Body(TestEvents.Json(action: "identity.login.succeeded", tenantId: null)));

        outcome.Should().Be(MessageOutcome.Acknowledge);
        (await StoredCountAsync()).Should().Be(1);
    }

    [TestCase("this is not json")]
    [TestCase("{\"eventId\": ")]
    [TestCase("")]
    [TestCase("[1, 2, 3]")]
    public async Task Handle_WhenTheBodyIsNotAJsonEvent_ThenItIsRejectedToTheDeadLetterQueue(string body)
    {
        var outcome = await HandleAsync("role.assigned", TestEvents.Body(body));

        outcome.Should().Be(MessageOutcome.Reject);
        (await StoredCountAsync()).Should().Be(0);
    }

    [Test]
    public async Task Handle_WhenTheBodyIsJsonNull_ThenItIsRejected()
    {
        (await HandleAsync("role.assigned", TestEvents.Body("null"))).Should().Be(MessageOutcome.Reject);
    }

    [Test]
    public async Task Handle_WhenTheEventDoesNotMatchItsRoutingKey_ThenItIsRejectedAndNotStored()
    {
        var outcome = await HandleAsync("role.unassigned", TestEvents.Body(TestEvents.Json(action: "role.assigned")));

        outcome.Should().Be(MessageOutcome.Reject);
        (await StoredCountAsync()).Should().Be(0);
    }

    [Test]
    public async Task Handle_WhenTheMetadataIsNotAnObject_ThenItIsRejected()
    {
        var outcome = await HandleAsync("role.assigned", TestEvents.Body(TestEvents.Json(metadata: "[1]")));

        outcome.Should().Be(MessageOutcome.Reject);
        (await StoredCountAsync()).Should().Be(0);
    }

    [Test]
    public async Task Handle_WhenTheRequiredFieldsAreMissing_ThenItIsRejected()
    {
        // No actor, target or metadata at all.
        var outcome = await HandleAsync("role.assigned", TestEvents.Body(
            $$"""{"eventId":"{{Guid.NewGuid()}}","occurredAt":"2026-09-26T10:00:00Z","tenantId":null,"action":"role.assigned"}"""));

        outcome.Should().Be(MessageOutcome.Reject);
        (await StoredCountAsync()).Should().Be(0);
    }

    [Test]
    public async Task Handle_WhenTheDatabaseFails_ThenItIsRetriedNotRejected()
    {
        // A transient failure must never dead-letter a perfectly good event.
        var db = NewContext();
        var handler = NewHandler(db);
        await db.DisposeAsync();

        var outcome = await handler.HandleAsync("role.assigned", TestEvents.Body(TestEvents.Json(tenantId: Guid.NewGuid())), CancellationToken.None);

        outcome.Should().Be(MessageOutcome.Retry);
    }

    [Test]
    public async Task Handle_WhenTheServiceIsShuttingDown_ThenItIsRetriedSoAnotherInstanceTakesIt()
    {
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();

        var outcome = await HandleAsync("role.assigned", TestEvents.Body(TestEvents.Json(tenantId: Guid.NewGuid())), cancelled.Token);

        outcome.Should().Be(MessageOutcome.Retry);
        (await StoredCountAsync()).Should().Be(0);
    }
}
