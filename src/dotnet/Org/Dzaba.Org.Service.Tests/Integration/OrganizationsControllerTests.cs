using Dzaba.ToMigrate;
using FluentAssertions;
using NUnit.Framework;

namespace Dzaba.Org.Service.Tests.Integration;

[TestFixture]
public class OrganizationsControllerTests : OrgControllerTestFixture
{
    [Test]
    public async Task CreateOrgAsync_WhenValidUserProvided_ThenOrgIsCreated()
    {
        var token = JwtSettings.GetTokenBuilder()
            .WithSubject("1234567890")
            .WithName("John Doe")
            .Build()
            .EncodeToString();

        var newOrg = await CreateTenantAsync("New Org", token).ConfigureAwait(false);
        newOrg.Should().NotBeNull();
    }
}