using Dzaba.HomeSecurity.LogsIngestion.Service.Authorization;
using Dzaba.HomeSecurity.MessageBroker.Contracts;
using Dzaba.HomeSecurity.TestUtils;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NUnit.Framework;

namespace Dzaba.HomeSecurity.LogsIngestion.Service.Tests;

/// <summary>
/// Base fixture for every controller test: swaps the real RabbitMQ-backed
/// IMessageBus for FakeMessageBus (this service has no database, so unlike
/// Org.Service.Tests there's no provider seam to swap). Device tokens are
/// validated under the default bearer scheme here, so the shared
/// AuthenticatedControllerTestFixture's default test signing key and device
/// token helpers apply as they are.
/// </summary>
public abstract class LogsIngestionServiceTestFixture : AuthenticatedControllerTestFixture<Program>
{
    protected FakeMessageBus MessageBus { get; private set; } = null!;

    protected override void OnConfigureServices(IServiceCollection services)
    {
        base.OnConfigureServices(services);

        MessageBus = new FakeMessageBus();
        services.RemoveAll<IMessageBus>();
        services.AddSingleton<IMessageBus>(MessageBus);
    }

    protected HttpClient CreateDeviceClient(Guid tenantId, Guid deviceId) =>
        CreateDeviceClient(tenantId, deviceId, LogsPolicies.LogsWrite);
}
