using Microsoft.EntityFrameworkCore;

namespace Dzaba.HomeSecurity.DbServer;

/// <summary>
/// The one seam a service's DbContext EF Core provider is configured
/// through. Exists specifically so integration tests can swap in the
/// InMemory provider via DI, without any service - the deployed production
/// binary - ever referencing the Microsoft.EntityFrameworkCore.InMemory
/// package. A config-flag branch inside the AddDbContext call would avoid the
/// double-AddDbContext provider conflict just as well, but only this DI seam
/// keeps the InMemory package out of a service's own dependency graph -
/// each service's test project (via Dzaba.HomeSecurity.TestUtils) supplies
/// the InMemory implementation instead.
/// </summary>
public interface IDbServerProvider
{
    void Configure(DbContextOptionsBuilder optionsBuilder, string connectionString);
}
