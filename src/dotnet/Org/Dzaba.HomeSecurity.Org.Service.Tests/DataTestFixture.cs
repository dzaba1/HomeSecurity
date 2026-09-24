using Dzaba.HomeSecurity.Org.Service.Data;
using Dzaba.HomeSecurity.Domain;
using Dzaba.TestUtils.Integration;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Dzaba.HomeSecurity.Org.Service.Tests;

/// <summary>
/// Registers AppDbContext against the EF Core InMemory provider, with a
/// fresh database name every time RegisterServices runs (IocTestFixture
/// calls it in [SetUp] before every test, so each test starts from an
/// empty database). Tests construct as many tenant-scoped AppDbContext
/// instances as they need directly, via <see cref="CreateContext"/>, rather
/// than resolving one fixed-tenant context from the container - query
/// filter tests need side-by-side contexts for different tenants sharing
/// the same InMemory database.
/// </summary>
public abstract class DataTestFixture : IocTestFixture
{
    protected string DatabaseName { get; private set; } = null!;

    protected override void RegisterServices(IServiceCollection services)
    {
        DatabaseName = Guid.NewGuid().ToString();

        // IocTestFixture already wired up Serilog console logging on
        // `services` before this method runs - reuse it for EF Core's own
        // query/change-tracking logs (debug detail + sensitive data logging
        // are safe here since it's test data, never production).
        services.AddDzabaHomeSecurityDataServices(
            (sp, options, connectionString) =>
            {
                options.UseInMemoryDatabase(connectionString)
                    .UseLoggerFactory(sp.GetRequiredService<ILoggerFactory>())
                    .EnableSensitiveDataLogging()
                    .EnableDetailedErrors();
            },
            _ => DatabaseName);
    }

    protected AppDbContext CreateContext(Guid tenantId)
    {
        var options = Container.GetRequiredService<DbContextOptions<AppDbContext>>();
        var context = new AppDbContext(options, new StaticTenantContext(tenantId));

        // Unlike relational providers (which apply HasData via migrations),
        // the InMemory provider only materializes HasData seed rows when the
        // store is explicitly created - it does not seed lazily on first
        // SaveChanges/query. EnsureCreated() is a no-op once the shared
        // InMemory database for this test has already been created.
        context.Database.EnsureCreated();

        return context;
    }
}
