using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace Dzaba.HomeSecurity.Authorization.OrgApi.Tests;

[TestFixture]
public sealed class BootstrapperTests
{
    [Test]
    public async Task AddDzabaHomeSecurityOrgApiPermissionSource_WhenResolved_ThenUsesTheAddressFromTheProvider()
    {
        var handler = new StubHttpMessageHandler(HttpStatusCode.OK, """{"permissionKeys":["router.view"]}""");
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers.Authorization = "Bearer t";
        var tenantId = Guid.NewGuid();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IHttpContextAccessor>(new HttpContextAccessor { HttpContext = httpContext });
        services.AddSingleton("http://configured-org.test/");
        services.AddDzabaHomeSecurityOrgApiPermissionSource(sp => new Uri(sp.GetRequiredService<string>()));
        // The typed client's name is the service type's name - swap only the
        // primary handler so the real resilience pipeline still wraps it.
        services.AddHttpClient(nameof(IPermissionSourceLoader)).ConfigurePrimaryHttpMessageHandler(() => handler);

        await using var provider = services.BuildServiceProvider();
        var result = await provider.GetRequiredService<IPermissionSourceLoader>()
            .LoadAsync(tenantId, "user-1", CancellationToken.None);

        result.PermissionKeys.Should().BeEquivalentTo("router.view");
        handler.Requests.Should().ContainSingle().Which.RequestUri.Should()
            .Be(new Uri($"http://configured-org.test/api/v1/orgs/{tenantId}/access-context"));
    }

    [TestCase("""{"permissionKeys":["router.view","router.manage"]}""")]
    [TestCase("""{"permissionKeys":[]}""")]
    public void AccessContextResponse_WhenGivenSchemaShapedJson_ThenRoundTripsWithoutLoss(string json)
    {
        var value = JsonSerializer.Deserialize<AccessContextResponse>(json);

        var roundTripped = JsonSerializer.Serialize(value);

        JsonNode.DeepEquals(JsonNode.Parse(json), JsonNode.Parse(roundTripped)).Should().BeTrue();
    }
}
