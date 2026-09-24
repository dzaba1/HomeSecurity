using System.Net.Http.Json;
using System.Text.Json;
using Dzaba.HomeSecurity.Authorization;
using Dzaba.HomeSecurity.DbServer;
using Dzaba.HomeSecurity.Devices.Service.Data;
using Dzaba.HomeSecurity.Domain;
using Dzaba.HomeSecurity.MessageBroker.Contracts;
using Dzaba.HomeSecurity.TestUtils;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using StackExchange.Redis;

namespace Dzaba.HomeSecurity.Devices.Service.Tests;

/// <summary>
/// Base fixture for every controller test: selects the InMemory EF Core
/// provider by swapping the shared DbServer library's IDbServerProvider seam
/// (see its doc comment for why that exists instead of a second, competing
/// AddDbContext(UseInMemoryDatabase(...)) call) with a fresh database name
/// per test; swaps IConnectionMultiplexer for FakeRedis's in-memory double,
/// IMessageBus for FakeMessageBus, and the Org.Service-backed permission
/// source for <see cref="FakePermissionSourceLoader"/> - no real Redis,
/// RabbitMQ or Org.Service needed. DataProtection uses an ephemeral
/// (in-memory) key provider instead of the Redis-backed key ring. JWT
/// authentication comes from the shared AuthenticatedControllerTestFixture.
/// </summary>
public abstract class DevicesServiceTestFixture : AuthenticatedControllerTestFixture<Program>
{
    // All set by SetUpFakes (a [SetUp], so it always runs before
    // OnConfigureConfiguration/OnConfigureServices' lazy first firing) and
    // read there, so the app and this fixture's helpers share the same
    // InMemory store (which EF Core's InMemory provider keys purely by name)
    // and the same fakes.
    private string databaseName = null!;

    protected FakeMessageBus MessageBus { get; private set; } = null!;

    protected FakePermissionSourceLoader PermissionSource { get; private set; } = null!;

    [SetUp]
    public void SetUpFakes()
    {
        databaseName = Guid.NewGuid().ToString();
        MessageBus = new FakeMessageBus();
        PermissionSource = new FakePermissionSourceLoader();
    }

    protected override void OnConfigureConfiguration(IConfigurationBuilder builder)
    {
        builder.AddInMemoryCollection(
        [
            // The InMemory provider doesn't support Database.Migrate() at
            // all, so Program.cs's dev-only auto-migrate block is disabled.
            new KeyValuePair<string, string?>("Database:AutoMigrate", "false"),
            new KeyValuePair<string, string?>("ConnectionStrings:DevicesDatabase", databaseName),
        ]);
    }

    protected override void OnConfigureServices(IServiceCollection services)
    {
        base.OnConfigureServices(services);

        services.RemoveAll<IDbServerProvider>();
        services.AddSingleton<IDbServerProvider, InMemoryDbServerProvider>();

        services.RemoveAll<IConnectionMultiplexer>();
        services.AddSingleton<IConnectionMultiplexer>(_ => FakeRedis.CreateConnectionMultiplexer());

        services.RemoveAll<IMessageBus>();
        services.AddSingleton<IMessageBus>(MessageBus);

        services.RemoveAll<IPermissionSourceLoader>();
        services.AddSingleton<IPermissionSourceLoader>(PermissionSource);

        services.AddDataProtection().UseEphemeralDataProtectionProvider();
    }

    /// <summary>
    /// A fresh DevicesDbContext bound to a specific tenant, for direct test
    /// setup/assertions - bypasses the HTTP API. Built independently of DI
    /// (not resolved via Container/a scope): a DI-resolved context's options
    /// capture a reference back to their originating scope for some internal
    /// services, which breaks once that scope is disposed.
    /// </summary>
    internal DevicesDbContext CreateDbContext(Guid tenantId)
    {
        var options = new DbContextOptionsBuilder<DevicesDbContext>().UseInMemoryDatabase(databaseName).Options;
        return new DevicesDbContext(options, new StaticTenantContext(tenantId));
    }

    protected static async Task<Guid> ExtractIdAsync(HttpResponseMessage response)
    {
        var body = await response.Content.ReadFromJsonAsync<JsonElement>().ConfigureAwait(false);
        return Guid.Parse(body.GetProperty("id").GetString()!);
    }
}
