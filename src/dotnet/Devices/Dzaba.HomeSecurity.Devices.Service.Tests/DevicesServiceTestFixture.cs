using Dzaba.HomeSecurity.DbServer;
using Dzaba.HomeSecurity.TestUtils;
using Dzaba.TestUtils.Integration.AspNet;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Dzaba.HomeSecurity.Devices.Service.Tests;

/// <summary>
/// Base fixture for every controller test: selects the InMemory EF Core
/// provider by swapping the shared DbServer library's IDbServerProvider seam
/// (see its doc comment for why that exists instead of a second, competing
/// AddDbContext(UseInMemoryDatabase(...)) call).
/// </summary>
public abstract class DevicesServiceTestFixture : ControllerTestFixture<Program>
{
    protected override void OnConfigureConfiguration(IConfigurationBuilder builder)
    {
        builder.AddInMemoryCollection(
        [
            // The InMemory provider doesn't support Database.Migrate() at
            // all, so Program.cs's dev-only auto-migrate block is disabled.
            new KeyValuePair<string, string?>("Database:AutoMigrate", "false"),
            new KeyValuePair<string, string?>("ConnectionStrings:DevicesDatabase", Guid.NewGuid().ToString()),
        ]);
    }

    protected override void OnConfigureServices(IServiceCollection services)
    {
        services.RemoveAll<IDbServerProvider>();
        services.AddSingleton<IDbServerProvider, InMemoryDbServerProvider>();
    }
}
