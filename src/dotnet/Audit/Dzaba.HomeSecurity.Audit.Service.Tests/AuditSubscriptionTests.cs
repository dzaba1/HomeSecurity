using System.Reflection;
using Dzaba.HomeSecurity.Audit.Service.Ingestion;
using Dzaba.HomeSecurity.DomainEvents;
using FluentAssertions;

namespace Dzaba.HomeSecurity.Audit.Service.Tests;

[TestFixture]
public sealed class AuditSubscriptionTests
{
    [Test]
    public void RoutingKeys_WhenBuilt_ThenEveryDomainEventNameIsBound()
    {
        // The trail records every event services publish: an event added to
        // DomainEventNames must reach it without anyone remembering to bind it.
        var names = typeof(DomainEventNames)
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(f => f.IsLiteral)
            .Select(f => (string)f.GetRawConstantValue()!)
            .ToList();

        AuditSubscription.RoutingKeys().Should().Contain(names);
    }

    [Test]
    public void RoutingKeys_WhenBuilt_ThenTheWholeIdentityFamilyIsBound()
    {
        AuditSubscription.RoutingKeys().Should().Contain("identity.#");
    }

    [Test]
    public void RoutingKeys_WhenBuilt_ThenThereAreNoDuplicates()
    {
        AuditSubscription.RoutingKeys().Should().OnlyHaveUniqueItems();
    }

    [Test]
    public void CreateOptions_WhenBuilt_ThenTheQueueIsTheServicesOwn()
    {
        var options = AuditSubscription.CreateOptions();

        options.Queue.Should().Be("homesecurity.audit-service");
        options.RoutingKeys.Should().NotBeEmpty();
        options.DeliveryLimit.Should().BeGreaterThan(1, "a message must get more than one attempt before being parked");
    }
}
