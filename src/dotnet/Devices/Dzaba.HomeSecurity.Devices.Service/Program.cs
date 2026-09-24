using Dzaba.HomeSecurity.Authorization;
using Dzaba.HomeSecurity.Authorization.OrgApi;
using Dzaba.HomeSecurity.Caching.Redis;
using Dzaba.HomeSecurity.DbServer;
using Dzaba.HomeSecurity.Devices.Service.Data;
using Dzaba.HomeSecurity.Devices.Service.Security;
using Dzaba.HomeSecurity.Devices.Service.Services;
using Dzaba.HomeSecurity.Domain;
using Dzaba.HomeSecurity.MessageBroker.RabbitMQ;
using Dzaba.HomeSecurity.Observability;
using Dzaba.HomeSecurity.WebApi;
using Dzaba.Utils.AspNet;
using Microsoft.EntityFrameworkCore;
using Serilog;

const string ServiceName = "Dzaba.HomeSecurity.Devices.Service";
var apiVersion = new Version(1, 0);

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseDzabaHomeSecuritySerilog(ServiceName);

builder.Services.AddHttpContextAccessor();
builder.Services.AddControllers();

builder.Services.AddDzabaHomeSecurityApiVersioning(apiVersion);
builder.Services.AddDzabaHomeSecuritySwaggerGen("Devices API", apiVersion);

builder.Services.AddJwtAuthentication(() => new JwtSettings
{
    Authority = builder.Configuration["Authentication:Keycloak:Authority"],
    Audience = builder.Configuration["Authentication:Keycloak:Audience"],
    RequireHttps = builder.Environment.IsProduction(),
    ValidateAudience = true,
    ValidateIssuer = true,
});

// One scoped MutableTenantContext behind both ITenantContext (what
// DevicesDbContext sees) and IMutableTenantContext (only the
// tenant-resolution middleware writes it).
builder.Services.AddDzabaHomeSecurityTenancy();

// Permission policies, and tenant membership, are answered from the shared
// access-context cache; on a miss they reach Org.Service (which owns
// Membership/UserRole/RolePermission) by relaying the caller's own bearer
// token - see docs/decisions/0016-devices-service-owns-its-own-database.md.
// Redis is a hard readiness dependency here (no "degraded" tag) - it will
// also hold the Data Protection key ring for router secrets.
builder.Services.AddDzabaHomeSecurityPermissionAuthorization(builder.Configuration);
builder.Services.AddDzabaHomeSecurityCachedTenantMembership();
builder.Services.AddDzabaHomeSecurityOrgApiPermissionSource(sp =>
    new Uri(sp.GetRequiredService<IConfiguration>()["Services:Org:BaseUrl"]
        ?? throw new InvalidOperationException("Missing Services:Org:BaseUrl")));
builder.Services.AddDzabaHomeSecurityRedisCache(sp =>
    sp.GetRequiredService<IConfiguration>().GetConnectionString("Redis")
        ?? throw new InvalidOperationException("Missing ConnectionStrings:Redis"));

// Devices.Service owns its own database, separate from Org.Service's -
// see docs/decisions/0016-devices-service-owns-its-own-database.md.
// Migrations live in this assembly. IDbServerProvider is the seam
// integration tests swap the InMemory provider in through.
builder.Services.AddDzabaHomeSecurityNpgsqlDbServer(typeof(Program).Assembly);
builder.Services.AddDbContext<DevicesDbContext>((sp, options) =>
{
    var connectionString = sp.GetRequiredService<IConfiguration>().GetConnectionString("DevicesDatabase")
        ?? throw new InvalidOperationException("Missing ConnectionStrings:DevicesDatabase");
    sp.GetRequiredService<IDbServerProvider>().Configure(options, connectionString);
});

// Router secrets are encrypted at rest with ASP.NET Core Data Protection
// (docs/architecture/15-router-credentials.md). The key ring lives in the
// same Redis (reusing the connection AddDzabaHomeSecurityRedisCache
// registered), so every replica behind a rolling deployment can decrypt what
// another replica encrypted, without a separate secrets store for v1.
builder.Services.AddDzabaHomeSecurityRedisDataProtection(ServiceName, "DataProtection-Keys:Devices");
builder.Services.AddTransient<IRouterSecretProtector, RouterSecretProtector>();

// Publishes router.changed (RoutersService) - a notification-only event with
// no consumer yet, published as a low-cost hedge for future integrations.
// See router_changed_message.json.
builder.Services.AddDzabaHomeSecurityRabbitMqMessageBus(
    builder.Configuration.GetConnectionString("RabbitMQ")
        ?? throw new InvalidOperationException("Missing ConnectionStrings:RabbitMQ"));

builder.Services.AddDzabaHomeSecurityHal();
builder.Services.AddTransient<IRoutersService, RoutersService>();
builder.Services.AddTransient<IDevicesService, DevicesService>();

builder.Services.AddHealthChecks()
    .AddNpgSql(sp => sp.GetRequiredService<IConfiguration>().GetConnectionString("DevicesDatabase")!, name: "postgres");

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
    var dbOptions = scope.ServiceProvider.GetRequiredService<DbContextOptions<DevicesDbContext>>();
    using var migrationContext = new DevicesDbContext(dbOptions, new StaticTenantContext(Guid.Empty));
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
app.UseAuthentication();
app.UseMiddleware<TenantResolutionMiddleware>();
app.UseAuthorization();
app.MapControllers();

// The Postgres and Redis checks are registered above - see
// MapDzabaHomeSecurityHealthChecks for the endpoint shape.
app.MapDzabaHomeSecurityHealthChecks();

app.UseDzabaHomeSecuritySwaggerUI("Devices API", apiVersion);

app.Run();

/// <summary>Exposed as a partial class so <c>WebApplicationFactory&lt;Program&gt;</c> can reference it from integration tests.</summary>
public partial class Program;
