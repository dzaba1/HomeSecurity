using Dzaba.HomeSecurity.Org.Service.Data;
using Microsoft.EntityFrameworkCore;

namespace Dzaba.HomeSecurity.Org.Service.Tests;

/// <summary>
/// The only place Microsoft.EntityFrameworkCore.InMemory is referenced -
/// Org.Service itself never takes on this dependency, see IDbServerProvider.
/// </summary>
internal sealed class InMemoryDbServerProvider : IDbServerProvider
{
    public void Configure(DbContextOptionsBuilder optionsBuilder, string connectionString)
    {
        ArgumentNullException.ThrowIfNull(optionsBuilder);
        ArgumentException.ThrowIfNullOrEmpty(connectionString);

        optionsBuilder.UseInMemoryDatabase(connectionString);
    }
}
