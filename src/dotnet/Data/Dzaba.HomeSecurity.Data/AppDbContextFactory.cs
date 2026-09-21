using Dzaba.HomeSecurity.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Dzaba.HomeSecurity.Data;

/// <summary>
/// Lets `dotnet ef migrations add` construct an AppDbContext without a
/// running app/DI container. The tenant value here is never used for
/// anything - migrations don't run queries through the model's query
/// filters.
/// </summary>
public sealed class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>();
        optionsBuilder.UseNpgsql("Host=localhost;Database=homesecurity;Username=homesecurity;Password=design-time-only");

        return new AppDbContext(optionsBuilder.Options, new StaticTenantContext(Guid.Empty));
    }
}
