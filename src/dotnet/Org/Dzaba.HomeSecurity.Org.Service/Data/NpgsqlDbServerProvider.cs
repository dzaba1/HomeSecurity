using Microsoft.EntityFrameworkCore;

namespace Dzaba.HomeSecurity.Org.Service.Data;

internal sealed class NpgsqlDbServerProvider : IDbServerProvider
{
    public void Configure(DbContextOptionsBuilder optionsBuilder, string connectionString)
    {
        ArgumentNullException.ThrowIfNull(optionsBuilder);
        ArgumentException.ThrowIfNullOrEmpty(connectionString);

        // Migrations live in this assembly, not Dzaba.HomeSecurity.Data
        // (which AppDbContext itself lives in, EF's default migrations
        // assembly) - keeps the shared Data project provider-agnostic; see
        // IDbServerProvider's doc comment and AppDbContextFactory.
        optionsBuilder.UseNpgsql(connectionString,
            b => b.MigrationsAssembly(typeof(NpgsqlDbServerProvider).Assembly.GetName().Name));
    }
}
