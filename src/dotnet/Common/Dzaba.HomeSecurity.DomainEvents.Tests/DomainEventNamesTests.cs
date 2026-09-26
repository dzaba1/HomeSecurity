using System.Reflection;
using System.Text.RegularExpressions;
using FluentAssertions;

namespace Dzaba.HomeSecurity.DomainEvents.Tests;

[TestFixture]
public class DomainEventNamesTests
{
    [Test]
    public void Names_WhenEnumerated_ThenEveryOneMatchesTheContractsActionPatternAndIsUnique()
    {
        // Mirrors the "pattern" of "action" in domain_event_message.json: a
        // name that doesn't match would be rejected by a schema-validating
        // subscriber (and is what the routing key is built from).
        var pattern = new Regex(@"^[a-z][a-z0-9_]*(\.[a-z][a-z0-9_]*)+$");
        var names = typeof(DomainEventNames)
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(f => f.IsLiteral)
            .Select(f => (string)f.GetRawConstantValue()!)
            .ToList();

        names.Should().NotBeEmpty();
        names.Should().OnlyContain(n => pattern.IsMatch(n));
        names.Should().OnlyHaveUniqueItems();
    }
}
