using System.Reflection;
using Dzaba.HomeSecurity.Audit.Service.Data;
using Dzaba.HomeSecurity.Audit.Service.Data.Entities;
using Dzaba.HomeSecurity.DomainEvents;
using FluentAssertions;

namespace Dzaba.HomeSecurity.Audit.Service.Tests;

[TestFixture]
public sealed class AuditCategoriesTests
{
    [TestCase("identity.login.succeeded", "Identity")]
    [TestCase("organization.created", "Access")]
    [TestCase("membership.added", "Access")]
    [TestCase("role.assigned", "Access")]
    [TestCase("router.credential.fetched", "Router")]
    [TestCase("device.updated", "Device")]
    [TestCase("dsr.export.requested", "DataSubjectRequest")]
    [TestCase("something.new", "Other")]
    public void For_WhenGivenAnEventName_ThenTheCategoryComesFromItsFirstSegment(string action, string expected)
    {
        // The category is a string here only because a public test method
        // can't take the service's internal enum as a parameter.
        AuditCategories.For(action).Should().Be(Enum.Parse<AuditCategory>(expected));
    }

    [Test]
    public void For_WhenTheNameHasNoDot_ThenTheWholeNameIsThePrefix()
    {
        AuditCategories.For("router").Should().Be(AuditCategory.Router);
    }

    [Test]
    public void For_WhenTheNameIsBlank_ThenItThrows()
    {
        var act = () => AuditCategories.For(" ");

        act.Should().Throw<ArgumentException>();
    }

    [Test]
    public void For_WhenGivenEveryEventNameServicesPublishToday_ThenNoneFallsIntoOther()
    {
        // A new event added to DomainEventNames without a category here would
        // silently be retained under "Other"'s rules.
        var names = typeof(DomainEventNames)
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(f => f.IsLiteral)
            .Select(f => (string)f.GetRawConstantValue()!)
            .ToList();

        names.Should().NotBeEmpty();
        names.Should().OnlyContain(n => AuditCategories.For(n) != AuditCategory.Other);
    }
}
