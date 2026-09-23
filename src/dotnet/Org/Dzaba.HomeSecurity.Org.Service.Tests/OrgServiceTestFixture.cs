using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Dzaba.AspNetUtils;
using Dzaba.HomeSecurity.Data;
using Dzaba.HomeSecurity.Domain;
using Dzaba.HomeSecurity.Org.Service.Data;
using Dzaba.TestUtils.Integration.AspNet;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.IdentityModel.Tokens;
using NUnit.Framework;
using StackExchange.Redis;

namespace Dzaba.HomeSecurity.Org.Service.Tests;

/// <summary>
/// Base fixture for every controller test: selects the InMemory EF Core
/// provider by swapping IDbServerProvider (Org.Service's own DI seam - see
/// its doc comment for why this exists instead of a second, competing
/// AddDbContext(UseInMemoryDatabase(...)) call) with a fresh database name
/// per test, and swaps IConnectionMultiplexer for FakeRedis's in-memory
/// double - no real Redis instance needed. JWT bearer options are overridden
/// with a symmetric test key (ControllerTestFixture's own AddMockedJwtSettings
/// helper assumes a DI-resolved JwtSettings, which Program.cs's
/// AddJwtAuthentication(Func&lt;JwtSettings&gt;) doesn't use - PostConfigure is
/// the mechanism that actually works against that wiring). DataProtection
/// uses an ephemeral (in-memory) key provider instead of the real host's
/// default disk-backed one - every test otherwise reads/writes the same
/// per-user key-ring file on disk, which is both needless I/O per test and,
/// once tests run in parallel (see the assembly-level [Parallelizable]),
/// a genuine race on that shared file.
/// </summary>
public abstract class OrgServiceTestFixture : ControllerTestFixture<Program>
{
    // Set by EnsureDatabaseCreatedAsync (a [SetUp], so it always runs before
    // OnConfigureConfiguration's lazy first firing - see that method's own
    // comment) and read here so the app's own DI-resolved AppDbContext and
    // this fixture's test-helper AppDbContext instances (built independently
    // of DI - see BuildDbContextOptions) share the same InMemory store,
    // which EF Core's InMemory provider keys purely by this name.
    private string databaseName = null!;

    protected override void OnConfigureConfiguration(IConfigurationBuilder builder)
    {
        builder.AddInMemoryCollection(
        [
            // The InMemory provider doesn't support Database.Migrate() at
            // all, so Program.cs's dev-only auto-migrate block must be
            // disabled here - this fixture's own EnsureCreatedAsync call
            // seeds the store instead.
            new KeyValuePair<string, string?>("Database:AutoMigrate", "false"),
            new KeyValuePair<string, string?>("ConnectionStrings:AppDatabase", databaseName),
        ]);
    }

    protected override void OnConfigureServices(IServiceCollection services)
    {
        services.RemoveAll<IDbServerProvider>();
        services.AddSingleton<IDbServerProvider, InMemoryDbServerProvider>();

        services.RemoveAll<IConnectionMultiplexer>();
        services.AddSingleton<IConnectionMultiplexer>(_ => FakeRedis.CreateConnectionMultiplexer());

        services.AddDataProtection().UseEphemeralDataProtectionProvider();

        services.PostConfigure<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme, jwtOptions =>
        {
            jwtOptions.Authority = null;
            jwtOptions.RequireHttpsMetadata = false;
            jwtOptions.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = false,
                ValidateAudience = false,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(JwtSettings.Settings.IssuerSigningKey)),
            };
        });
    }

    [SetUp]
    public async Task EnsureDatabaseCreatedAsync()
    {
        // Must be the first statement: OnConfigureConfiguration (lazily
        // triggered by the first host-build-causing access, e.g. this
        // fixture's own CreateAuthenticatedClient/CreateClient calls in the
        // test body) reads this field.
        databaseName = Guid.NewGuid().ToString();

        using var db = CreateDbContext(Guid.Empty);
        // InMemory only materializes HasData seed rows (the permission
        // catalog/system roles) once EnsureCreated runs - same reasoning as
        // Data.Tests' DataTestFixture.
        await db.Database.EnsureCreatedAsync();
    }

    /// <summary>
    /// A fresh AppDbContext bound to a specific tenant, for direct test
    /// setup/assertions - bypasses the HTTP API. Built independently of DI
    /// (not resolved via Container/a scope): AddDbContext's
    /// DbContextOptions&lt;AppDbContext&gt; captures a reference back to its
    /// originating scope for some internal services (e.g. logging), which
    /// breaks once that scope is disposed - building the options standalone
    /// avoids that entirely, same as Data.Tests' DataTestFixture and
    /// AppDbContextFactory already do.
    /// </summary>
    protected AppDbContext CreateDbContext(Guid tenantId)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(databaseName).Options;
        return new AppDbContext(options, new StaticTenantContext(tenantId));
    }

    protected string CreateToken(string userId) =>
        JwtSettings.GetTokenBuilder().WithSubject(userId).Build().EncodeToString();

    protected HttpClient CreateAuthenticatedClient(string userId)
    {
        var client = CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", CreateToken(userId));
        return client;
    }

    protected static async Task<Guid> ExtractIdAsync(HttpResponseMessage response)
    {
        var body = await response.Content.ReadFromJsonAsync<JsonElement>().ConfigureAwait(false);
        return Guid.Parse(body.GetProperty("id").GetString()!);
    }

    protected async Task<Guid> CreateOrgAsync(HttpClient client, string identifier)
    {
        var response = await client.PostAsJsonAsync("/api/v1/orgs", new { identifier, name = identifier }).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        return await ExtractIdAsync(response).ConfigureAwait(false);
    }
}
