using System.Text.Json;
using AutoFixture;
using Dzaba.HomeSecurity.Domain;
using Dzaba.HomeSecurity.TestUtils;
using Dzaba.TestUtils;
using FluentAssertions;
using Moq;

namespace Dzaba.HomeSecurity.DomainEvents.Tests;

[TestFixture]
public class DomainEventPublisherTests : AutoFixtureTestFixture
{
    private FakeMessageBus bus = null!;
    private Mock<ICurrentActor> actor = null!;
    private Mock<ITenantContext> tenantContext = null!;

    [SetUp]
    public void SetUp()
    {
        bus = new FakeMessageBus();
        Fixture.Register<MessageBroker.Contracts.IMessageBus>(() => bus);

        actor = Fixture.FreezeMock<ICurrentActor>();
        actor.Setup(a => a.GetCurrent()).Returns(new Actor { Type = ActorType.User, Id = "user-1" });

        tenantContext = Fixture.FreezeMock<ITenantContext>();
    }

    private DomainEventPublisher CreatePublisher(Guid tenantId)
    {
        tenantContext.SetupGet(t => t.TenantId).Returns(tenantId);
        return Fixture.Create<DomainEventPublisher>();
    }

    private DomainEventMessage SinglePublished(string routingKey)
    {
        var published = bus.Published.Should().ContainSingle().Subject;
        published.RoutingKey.Should().Be(routingKey);
        return published.Message.Should().BeOfType<DomainEventMessage>().Subject;
    }

    [Test]
    public async Task PublishAsync_WhenCalled_ThenTheRoutingKeyIsTheEventNameAndTheEnvelopeIsFilled()
    {
        var tenantId = Guid.NewGuid();

        var eventId = await CreatePublisher(tenantId).PublishAsync(
            DomainEventNames.RoleAssigned, DomainEventTargetTypes.UserRole, "user-2:role-1",
            new Dictionary<string, object?> { ["roleName"] = "Admin" });

        var message = SinglePublished("role.assigned");
        message.EventId.Should().Be(eventId).And.NotBe(Guid.Empty);
        message.OccurredAt.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromMinutes(1));
        message.TenantId.Should().Be(tenantId);
        message.Actor.Type.Should().Be(ActorType.User);
        message.Actor.Id.Should().Be("user-1");
        message.Action.Should().Be("role.assigned");
        message.Target.Type.Should().Be("UserRole");
        message.Target.Id.Should().Be("user-2:role-1");
    }

    [Test]
    public async Task PublishAsync_WhenTheRequestHasNoTenant_ThenTheTenantIdIsNull()
    {
        await CreatePublisher(Guid.Empty).PublishAsync(
            DomainEventNames.OrganizationCreated, DomainEventTargetTypes.Organization, "org-1");

        SinglePublished("organization.created").TenantId.Should().BeNull();
    }

    [Test]
    public async Task PublishAsync_WhenATenantIsPassedExplicitly_ThenItWinsOverTheRequestsTenant()
    {
        var explicitTenant = Guid.NewGuid();

        await CreatePublisher(Guid.NewGuid()).PublishAsync(
            DomainEventNames.RouterCredentialFetched, DomainEventTargetTypes.Router, "router-1",
            tenantId: explicitTenant);

        SinglePublished("router.credential.fetched").TenantId.Should().Be(explicitTenant);
    }

    [Test]
    public async Task PublishAsync_WhenNoMetadataIsGiven_ThenItIsAnEmptyObjectNotNull()
    {
        await CreatePublisher(Guid.NewGuid()).PublishAsync(
            DomainEventNames.MembershipAdded, DomainEventTargetTypes.Membership, "user-2");

        var json = JsonSerializer.Serialize(SinglePublished("membership.added"));
        JsonDocument.Parse(json).RootElement.GetProperty("metadata").ValueKind.Should().Be(JsonValueKind.Object);
    }

    [Test]
    public async Task PublishAsync_WhenMetadataIsGiven_ThenItSurvivesSerializationAsTheExtendedData()
    {
        await CreatePublisher(Guid.NewGuid()).PublishAsync(
            DomainEventNames.RoleUpdated, DomainEventTargetTypes.Role, "role-1",
            new Dictionary<string, object?>
            {
                ["name"] = "Ops",
                ["addedPermissionKeys"] = new[] { "audit.view" },
            });

        var metadata = JsonDocument.Parse(JsonSerializer.Serialize(SinglePublished("role.updated")))
            .RootElement.GetProperty("metadata");
        metadata.GetProperty("name").GetString().Should().Be("Ops");
        metadata.GetProperty("addedPermissionKeys")[0].GetString().Should().Be("audit.view");
    }

    [TestCase("")]
    [TestCase(" ")]
    public async Task PublishAsync_WhenAnArgumentIsBlank_ThenItThrowsAndPublishesNothing(string blank)
    {
        var publisher = CreatePublisher(Guid.NewGuid());

        var act = async () => await publisher.PublishAsync(blank, "T", "1");

        await act.Should().ThrowAsync<ArgumentException>();
        bus.Published.Should().BeEmpty();
    }
}
