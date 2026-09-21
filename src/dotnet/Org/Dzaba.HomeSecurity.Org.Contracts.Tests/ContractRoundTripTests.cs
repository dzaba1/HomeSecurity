using System.Text.Json;
using System.Text.Json.Nodes;
using FluentAssertions;
using NUnit.Framework;

namespace Dzaba.HomeSecurity.Org.Contracts.Tests;

[TestFixture]
public class ContractRoundTripTests
{
    [TestCase(typeof(Organization), """{"id":"11111111-1111-1111-1111-111111111111","identifier":"acme","name":"Acme"}""")]
    [TestCase(typeof(CreateOrganization), """{"identifier":"acme","name":"Acme"}""")]
    [TestCase(typeof(Permission), """{"key":"device.view","description":"View devices"}""")]
    [TestCase(typeof(Role), """{"id":"11111111-1111-1111-1111-111111111111","tenantId":null,"name":"Owner","permissionKeys":["device.view","device.delete"]}""")]
    [TestCase(typeof(Role), """{"id":"11111111-1111-1111-1111-111111111111","tenantId":"22222222-2222-2222-2222-222222222222","name":"Custom","permissionKeys":["device.view"]}""")]
    [TestCase(typeof(CreateRole), """{"name":"Custom","permissionKeys":["device.view"]}""")]
    [TestCase(typeof(Membership), """{"organizationId":"11111111-1111-1111-1111-111111111111","userId":"user-1"}""")]
    [TestCase(typeof(CreateMembership), """{"userId":"user-1"}""")]
    [TestCase(typeof(UserRoleAssignment), """{"userId":"user-1","tenantId":"11111111-1111-1111-1111-111111111111","roleId":"22222222-2222-2222-2222-222222222222"}""")]
    [TestCase(typeof(AssignUserRole), """{"userId":"user-1","roleId":"22222222-2222-2222-2222-222222222222"}""")]
    public void Deserialize_WhenGivenSchemaShapedJson_ThenRoundTripsWithoutLoss(Type contractType, string json)
    {
        var value = JsonSerializer.Deserialize(json, contractType);

        var roundTripped = JsonSerializer.Serialize(value, contractType);

        JsonNode.DeepEquals(JsonNode.Parse(json), JsonNode.Parse(roundTripped)).Should().BeTrue();
    }

    [Test]
    public void Deserialize_WhenRoleTenantIdIsNull_ThenItMeansASystemDefaultRole()
    {
        var role = JsonSerializer.Deserialize<Role>(
            """{"id":"11111111-1111-1111-1111-111111111111","tenantId":null,"name":"Owner","permissionKeys":[]}""");

        role!.TenantId.Should().BeNull();
    }

    [Test]
    public void Deserialize_WhenRoleTenantIdIsPresent_ThenItIsACustomTenantRole()
    {
        var role = JsonSerializer.Deserialize<Role>(
            """{"id":"11111111-1111-1111-1111-111111111111","tenantId":"22222222-2222-2222-2222-222222222222","name":"Custom","permissionKeys":[]}""");

        role!.TenantId.Should().Be(Guid.Parse("22222222-2222-2222-2222-222222222222"));
    }
}
