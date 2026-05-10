using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Dzaba.Org.Contracts;
using Dzaba.TestUtils.Integration.AspNet;
using Dzaba.ToMigrate;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Dzaba.Org.Service.Tests.Integration;

public abstract class OrgControllerTestFixture : ControllerTestFixture<Program>
{
    protected JwtMockedSettings JwtSettings { get; } = new JwtMockedSettings();

    protected override void OnConfigureConfiguration(IConfigurationBuilder builder)
    {
        builder.AddInMemoryCollection(new Dictionary<string, string>
        {
            ["ConnectionStrings:OrgDatabase"] = Guid.NewGuid().ToString()
        });
    }

    protected override void OnConfigureServices(IServiceCollection services)
    {
        services.RemoveAll<IDbServerProvider>();
        services.AddTransient<IDbServerProvider, InMemoryDbServerProvider>();

        JwtSettings.AddMockedJwtSettings(services);
    }

    protected async Task<Organization> CreateTenantAsync(string name, string token)
    {
        var client = CreateClient();

        var requestBody = new CreateOrganization
        {
            Name = name
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, "/v1/organizations")
        {
            Content = JsonContent.Create(requestBody),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var resp = await client.SendAsync(request).ConfigureAwait(false);
        var respStr = await AssertAsync(resp).ConfigureAwait(false);
        return JsonSerializer.Deserialize<Organization>(respStr);
    }
}