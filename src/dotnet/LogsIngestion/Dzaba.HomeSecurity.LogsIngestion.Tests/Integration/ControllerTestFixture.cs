using Dzaba.IntegrationTestUtils;
using EasyNetQ;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NUnit.Framework;

namespace Dzaba.HomeSecurity.LogsIngestion.Tests.Integration;

public abstract class ControllerTestFixture
{
    public static readonly string Authority = "http://test";
    public static readonly string Audience = "home-security";
    public static readonly string IssuerSigningKey = "a-string-secret-at-least-256-bits-long";

    private WebApplicationFactory<Program> factory;
    private Action<IConfigurationBuilder> configBuilderCallback = null;

    [SetUp]
    public void SetupBuilder()
    {
        factory = new WebApplicationFactory<Program>().WithWebHostBuilder(b =>
        {
            b.ConfigureAppConfiguration((context, builder) =>
            {
                if (configBuilderCallback != null)
                {
                    configBuilderCallback(builder);
                }
            })
            .ConfigureTestServices(services =>
            {
                services.RemoveAll<IConfiguration>();
                services.RemoveAll<IBus>();

                var config = new ConfigurationBuilder()
                    .AddInMemoryCollection(new Dictionary<string, string>
                    {
                        ["JwtAuth:Authority"] = Authority,
                        ["JwtAuth:Audience"] = Audience,
                        ["JwtAuth:ValidateAudience"] = "false",
                        ["JwtAuth:ValidateIssuer"] = "false",
                        ["JwtAuth:IssuerSigningKey"] = IssuerSigningKey,
                    })
                    .Build();
                services.AddSingleton<IConfiguration>(config);

                services.AddSingleton<IBus, InMemoryBus>();

                services.AddSerilogConsoleLogging();
            });
        });
    }

    [TearDown]
    public void CleanupFactory()
    {
        factory?.Dispose();
    }

    protected HttpClient CreateClient(Action<IConfigurationBuilder> configBuilderCallback = null)
    {
        this.configBuilderCallback = configBuilderCallback;
        return factory.CreateClient();
    }

    protected async Task<string> AssertAsync(HttpResponseMessage resp)
    {
        resp.Should().NotBeNull();
        var respStr = await resp.Content.ReadAsStringAsync().ConfigureAwait(false);

        this.Invoking(_ => resp.EnsureSuccessStatusCode())
            .Should().NotThrow(respStr);

        return respStr;
    }
}
