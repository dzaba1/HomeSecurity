using Dzaba.IntegrationTestUtils;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using NUnit.Framework;

namespace Dzaba.HomeSecurity.LogsIngestion.Tests.Integration;

public abstract class ControllerTestFixture
{
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
