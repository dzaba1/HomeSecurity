using Dzaba.HomeSecurity.DbServer;
using Microsoft.EntityFrameworkCore;

namespace Dzaba.HomeSecurity.TestUtils;

/// <summary>
/// The only place Microsoft.EntityFrameworkCore.InMemory is referenced on
/// behalf of a service's own tests - the services themselves never take on
/// this dependency, see IDbServerProvider. A test fixture swaps this in for
/// the real Npgsql-backed provider via DI.
/// </summary>
public sealed class InMemoryDbServerProvider : IDbServerProvider
{
    public void Configure(DbContextOptionsBuilder optionsBuilder, string connectionString)
    {
        ArgumentNullException.ThrowIfNull(optionsBuilder);
        ArgumentException.ThrowIfNullOrEmpty(connectionString);

        optionsBuilder.UseInMemoryDatabase(connectionString);
    }
}
