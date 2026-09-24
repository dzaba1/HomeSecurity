using FluentAssertions;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.DataProtection.KeyManagement;
using Microsoft.AspNetCore.DataProtection.StackExchangeRedis;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Moq;
using StackExchange.Redis;

namespace Dzaba.HomeSecurity.Caching.Redis.Tests;

[TestFixture]
public sealed class DataProtectionBootstrapperTests
{
    private ServiceProvider BuildProvider(string applicationName = "test-app", string keyRingName = "test-keys")
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(Mock.Of<IConnectionMultiplexer>());
        services.AddDzabaHomeSecurityRedisDataProtection(applicationName, keyRingName);
        return services.BuildServiceProvider();
    }

    [Test]
    public void AddDzabaHomeSecurityRedisDataProtection_WhenResolved_ThenTheKeyRingIsStoredInRedis()
    {
        using var provider = BuildProvider();

        var options = provider.GetRequiredService<IOptions<KeyManagementOptions>>().Value;

        options.XmlRepository.Should().BeOfType<RedisXmlRepository>();
    }

    [Test]
    public void AddDzabaHomeSecurityRedisDataProtection_WhenResolved_ThenUsesTheGivenApplicationName()
    {
        using var provider = BuildProvider(applicationName: "devices-app");

        var options = provider.GetRequiredService<IOptions<DataProtectionOptions>>().Value;

        options.ApplicationDiscriminator.Should().Be("devices-app");
    }

    [TestCase("", "keys")]
    [TestCase("app", "")]
    public void AddDzabaHomeSecurityRedisDataProtection_WhenAnArgumentIsEmpty_ThenThrows(string applicationName, string keyRingName)
    {
        var act = () => new ServiceCollection().AddDzabaHomeSecurityRedisDataProtection(applicationName, keyRingName);

        act.Should().Throw<ArgumentException>();
    }
}
