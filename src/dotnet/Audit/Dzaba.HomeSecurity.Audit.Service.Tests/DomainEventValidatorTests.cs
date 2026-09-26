using System.Text.Json;
using Dzaba.HomeSecurity.Audit.Service.Ingestion;
using Dzaba.HomeSecurity.DomainEvents;
using FluentAssertions;

namespace Dzaba.HomeSecurity.Audit.Service.Tests;

[TestFixture]
public sealed class DomainEventValidatorTests
{
    private static DomainEventMessage Valid() => TestEvents.Message(tenantId: Guid.NewGuid());

    [Test]
    public void Validate_WhenTheMessageIsWellFormed_ThenThereIsNoProblem()
    {
        DomainEventValidator.Validate(Valid(), "role.assigned").Should().BeNull();
    }

    [Test]
    public void Validate_WhenTheEventHasNoTenant_ThenThereIsNoProblem()
    {
        // A pre-tenant event, e.g. a login.
        DomainEventValidator.Validate(TestEvents.Message(tenantId: null), "role.assigned").Should().BeNull();
    }

    [Test]
    public void Validate_WhenTheMetadataIsAnEmptyObject_ThenThereIsNoProblem()
    {
        DomainEventValidator.Validate(TestEvents.Message(metadata: "{}"), "role.assigned").Should().BeNull();
    }

    [Test]
    public void Validate_WhenTheBodyWasJsonNull_ThenItIsRejected()
    {
        DomainEventValidator.Validate(null, "role.assigned").Should().Contain("empty or null");
    }

    private static IEnumerable<TestCaseData> BrokenMessages()
    {
        yield return Case("eventId missing", m => m.EventId = Guid.Empty, "eventId");
        yield return Case("occurredAt missing", m => m.OccurredAt = default, "occurredAt");
        yield return Case("tenantId all zeros", m => m.TenantId = Guid.Empty, "tenantId");
        yield return Case("action empty", m => m.Action = "", "action");
        yield return Case("action upper case", m => m.Action = "Role.Assigned", "action");
        yield return Case("action without a dot", m => m.Action = "roleassigned", "action");
        yield return Case("action too long", m => m.Action = "a." + new string('b', DomainEventValidator.MaxActionLength), "action");
        yield return Case("actor unknown type", m => m.Actor.Type = (ActorType)99, "actor");
        yield return Case("actor id blank", m => m.Actor.Id = "  ", "actor");
        yield return Case("actor id too long", m => m.Actor.Id = new string('x', DomainEventValidator.MaxActorIdLength + 1), "actor");
        yield return Case("actor null", m => m.Actor = null!, "actor");
        yield return Case("target type blank", m => m.Target.Type = "", "target");
        yield return Case("target id too long", m => m.Target.Id = new string('x', DomainEventValidator.MaxTargetIdLength + 1), "target");
        yield return Case("target null", m => m.Target = null!, "target");
        yield return Case("metadata null", m => m.Metadata = null!, "metadata");
        yield return Case("metadata the generated default", m => m.Metadata = new object(), "metadata");
        yield return Case("metadata a JSON array", m => m.Metadata = JsonDocument.Parse("[1]").RootElement.Clone(), "metadata");
        yield return Case("metadata a JSON string", m => m.Metadata = JsonDocument.Parse("\"x\"").RootElement.Clone(), "metadata");
    }

    private static TestCaseData Case(string name, Action<DomainEventMessage> break_, string expectedMention) =>
        new TestCaseData(break_, expectedMention).SetName($"Validate_When_{name.Replace(' ', '_')}_ThenItIsRejected");

    [TestCaseSource(nameof(BrokenMessages))]
    public void Validate_WhenAPartOfTheEnvelopeIsWrong_ThenItIsRejectedAndSaysWhich(Action<DomainEventMessage> breakIt, string mention)
    {
        var message = Valid();
        breakIt(message);

        DomainEventValidator.Validate(message, "role.assigned").Should().Contain(mention);
    }

    [Test]
    public void Validate_WhenTheActionIsNotTheRoutingKey_ThenItIsRejected()
    {
        DomainEventValidator.Validate(Valid(), "role.unassigned").Should().Contain("routing key");
    }
}
