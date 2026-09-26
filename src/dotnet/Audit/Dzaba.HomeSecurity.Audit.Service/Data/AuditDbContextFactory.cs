using Dzaba.HomeSecurity.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Dzaba.HomeSecurity.Audit.Service.Data;

/// <summary>
/// Lets `dotnet ef migrations add` construct an AuditDbContext without a
/// running app/DI container. The tenant value here is never used for
/// anything - migrations don't run queries through the model's query
/// filters.
/// </summary>
internal sealed class AuditDbContextFactory : IDesignTimeDbContextFactory<AuditDbContext>
{
    public AuditDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<AuditDbContext>();
        optionsBuilder.UseNpgsql(
            "Host=localhost;Database=homesecurity_audit;Username=homesecurity;Password=design-time-only",
            b => b.MigrationsAssembly(typeof(AuditDbContextFactory).Assembly.GetName().Name));

        return new AuditDbContext(optionsBuilder.Options, new StaticTenantContext(Guid.Empty));
    }
}
