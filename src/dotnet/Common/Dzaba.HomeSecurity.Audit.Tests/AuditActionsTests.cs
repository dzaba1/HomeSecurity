using System.Reflection;
using System.Text.RegularExpressions;
using FluentAssertions;

namespace Dzaba.HomeSecurity.Audit.Tests;

[TestFixture]
public class AuditActionsTests
{
    [Test]
    public void RoutingKey_WhenGivenAnAction_ThenItIsPrefixedSoAuditServiceCanBindAuditHash()
    {
        AuditActions.RoutingKey(AuditActions.RoleAssigned).Should().Be("audit.role.assigned");
    }

    [Test]
    public void RoutingKey_WhenTheActionIsBlank_ThenItThrows()
    {
        var act = () => AuditActions.RoutingKey(" ");

        act.Should().Throw<ArgumentException>();
    }

    [Test]
    public void Actions_WhenEnumerated_ThenEveryOneMatchesTheContractsActionPatternAndIsUnique()
    {
        // Mirrors the "pattern" of "action" in audit_event_message.json: an
        // action that doesn't match would be rejected by the consumer.
        var pattern = new Regex(@"^[a-z][a-z0-9_]*(\.[a-z][a-z0-9_]*)+$");
        var actions = typeof(AuditActions)
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(f => f is { IsLiteral: true } && f.Name != nameof(AuditActions.RoutingKeyPrefix))
            .Select(f => (string)f.GetRawConstantValue()!)
            .ToList();

        actions.Should().NotBeEmpty();
        actions.Should().OnlyContain(a => pattern.IsMatch(a));
        actions.Should().OnlyHaveUniqueItems();
    }
}
