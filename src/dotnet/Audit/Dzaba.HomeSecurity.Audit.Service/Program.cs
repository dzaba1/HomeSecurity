using Dzaba.HomeSecurity.Audit.Service.Data;
using Dzaba.HomeSecurity.Authorization;
using Dzaba.HomeSecurity.DbServer;
using Dzaba.HomeSecurity.Domain;
using Dzaba.HomeSecurity.Observability;
using Dzaba.HomeSecurity.WebApi;
using Microsoft.EntityFrameworkCore;
using Serilog;

const string ServiceName = "Dzaba.HomeSecurity.Audit.Service";

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseDzabaHomeSecuritySerilog(ServiceName);

builder.Services.AddControllers();

// One scoped MutableTenantContext behind both ITenantContext (what
// AuditDbContext sees) and IMutableTenantContext.
builder.Services.AddDzabaHomeSecurityTenancy();

// Audit.Service owns its own database, separate from every other service's -
// see docs/decisions/0016-devices-service-owns-its-own-database.md. Migrations
// live in this assembly. IDbServerProvider is the seam integration tests swap
// the InMemory provider in through.
builder.Services.AddDzabaHomeSecurityNpgsqlDbServer(typeof(Program).Assembly);
builder.Services.AddDbContext<AuditDbContext>((sp, options) =>
{
    var connectionString = sp.GetRequiredService<IConfiguration>().GetConnectionString("AuditDatabase")
        ?? throw new InvalidOperationException("Missing ConnectionStrings:AuditDatabase");
    sp.GetRequiredService<IDbServerProvider>().Configure(options, connectionString);
});

builder.Services.AddHealthChecks()
    .AddNpgSql(sp => sp.GetRequiredService<IConfiguration>().GetConnectionString("AuditDatabase")!, name: "postgres");

builder.Services.AddDzabaHomeSecurityOpenTelemetry(ServiceName,
    () => new Uri(builder.Configuration["OpenTelemetry:OtlpEndpoint"] ?? "http://localhost:4317"));

var app = builder.Build();

// Dev-only convenience: a single local instance can't race itself.
// Everywhere else, applying migrations is a deliberate, separate deployment
// step - not something Program.cs does on every pod start, since concurrent
// Database.Migrate() calls across the >=2 replicas ADR-0011 requires would
// race on the same schema change. Also gated by config (default true), so
// WebApplicationFactory-based integration tests - which default to
// "Development" - can opt out when running against a non-relational
// provider (e.g. EF Core InMemory) that doesn't support Migrate() at all.
if (app.Environment.IsDevelopment() && builder.Configuration.GetValue("Database:AutoMigrate", true))
{
    // Constructed directly with StaticTenantContext, not resolved from DI:
    // there's no HTTP request during startup to have set the scoped tenant
    // context, and migrations don't run through the model's query filters.
    using var scope = app.Services.CreateScope();
    var dbOptions = scope.ServiceProvider.GetRequiredService<DbContextOptions<AuditDbContext>>();
    using var migrationContext = new AuditDbContext(dbOptions, new StaticTenantContext(Guid.Empty));
    migrationContext.Database.Migrate();
}

app.UseSerilogRequestLogging();

// Defaults to true (secure by default per docs/README.md's HTTPS-only
// principle). Overridable via config/env var so environments that terminate
// TLS in front of this service - e.g. the local docker-compose stack - can
// disable the redirect instead of 307-looping to a TLS port the container
// doesn't serve.
if (builder.Configuration.GetValue("Security:UseHttpsRedirection", true))
{
    app.UseHttpsRedirection();
}

app.UseRouting();
app.MapControllers();

// The Postgres check is registered above - see MapDzabaHomeSecurityHealthChecks
// for the endpoint shape.
app.MapDzabaHomeSecurityHealthChecks();

app.Run();

/// <summary>Exposed as a partial class so <c>WebApplicationFactory&lt;Program&gt;</c> can reference it from integration tests.</summary>
public partial class Program;
