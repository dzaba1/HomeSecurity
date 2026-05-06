using Microsoft.EntityFrameworkCore;

namespace Dzaba.HomeSecurity.LogsIngestion.Tests.Integration;

internal sealed class InMemoryDbServerProvider : IDbServerProvider
{
    public void Configure(DbContextOptionsBuilder optionsBuilder, string connectionString)
    {
        ArgumentNullException.ThrowIfNull(optionsBuilder);
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);

        optionsBuilder.UseInMemoryDatabase(connectionString);
    }
}
