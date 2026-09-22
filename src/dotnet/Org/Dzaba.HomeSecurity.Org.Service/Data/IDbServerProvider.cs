using Microsoft.EntityFrameworkCore;

namespace Dzaba.HomeSecurity.Org.Service.Data;

/// <summary>
/// The one seam AppDbContext's EF Core provider is configured through.
/// Exists specifically so integration tests can swap in the InMemory
/// provider via DI, without Org.Service itself - the deployed production
/// binary - ever referencing the Microsoft.EntityFrameworkCore.InMemory
/// package. A config-flag branch inside the AddDzabaHomeSecurityDataServices
/// call would avoid the double-AddDbContext provider conflict just as well,
/// but only this DI seam keeps the InMemory package out of Org.Service's
/// own dependency graph - Org.Service.Tests provides its own implementation.
/// </summary>
public interface IDbServerProvider
{
    void Configure(DbContextOptionsBuilder optionsBuilder, string connectionString);
}
