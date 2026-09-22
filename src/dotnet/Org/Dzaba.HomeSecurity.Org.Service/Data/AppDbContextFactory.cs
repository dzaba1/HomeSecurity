using Dzaba.HomeSecurity.Data;
using Dzaba.HomeSecurity.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Dzaba.HomeSecurity.Org.Service.Data;

/// <summary>
/// Lets `dotnet ef migrations add` construct an AppDbContext without a
/// running app/DI container. The tenant value here is never used for
/// anything - migrations don't run queries through the model's query
/// filters. Lives here, not in Dzaba.HomeSecurity.Data, so the shared Data
/// project can stay provider-agnostic (no Npgsql reference) - Org.Service
/// is where the production provider choice actually belongs, and where
/// migrations are generated/applied from.
/// </summary>
public sealed class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>();
        optionsBuilder.UseNpgsql(
            "Host=localhost;Database=homesecurity;Username=homesecurity;Password=design-time-only",
            b => b.MigrationsAssembly(typeof(AppDbContextFactory).Assembly.GetName().Name));

        return new AppDbContext(optionsBuilder.Options, new StaticTenantContext(Guid.Empty));
    }
}
