using Dzaba.HomeSecurity.Authorization;
using Dzaba.HomeSecurity.Caching.Redis;
using Dzaba.HomeSecurity.Data;
using Dzaba.HomeSecurity.Domain;
using Dzaba.HomeSecurity.MessageBroker.RabbitMQ;
using Dzaba.HomeSecurity.Observability;
using Dzaba.HomeSecurity.Org.Service.Data;
using Dzaba.HomeSecurity.Org.Service.Services;
using Dzaba.HomeSecurity.Org.Service.Tenancy;
using Dzaba.HomeSecurity.WebApi;
using Dzaba.Utils.AspNet;
using Microsoft.EntityFrameworkCore;
using Serilog;

const string ServiceName = "Dzaba.HomeSecurity.Org.Service";
var apiVersion = new Version(1, 0);

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseDzabaHomeSecuritySerilog(ServiceName);

builder.Services.AddHttpContextAccessor();
builder.Services.AddControllers();

builder.Services.AddDzabaHomeSecurityApiVersioning(apiVersion);
builder.Services.AddDzabaHomeSecuritySwaggerGen("Org API", apiVersion);

builder.Services.AddJwtAuthentication(() => new JwtSettings
{
    Authority = builder.Configuration["Authentication:Keycloak:Authority"],
    Audience = builder.Configuration["Authentication:Keycloak:Audience"],
    RequireHttps = builder.Environment.IsProduction(),
    ValidateAudience = true,
    ValidateIssuer = true,
});

builder.Services.AddDzabaHomeSecurityPermissionAuthorization(builder.Configuration);
// Org.Service owns Membership/UserRole/RolePermission directly, so it
// loads access context from its own tables - see LocalDbPermissionSourceLoader's
// doc comment for how a service that doesn't own this data would differ.
builder.Services.AddTransient<IPermissionSourceLoader, LocalDbPermissionSourceLoader>();

// One scoped HttpRequestTenantContext instance behind both interfaces - the
// write side (IMutableTenantContext) is used only by
// TenantResolutionMiddleware and org creation; everything else, including
// AppDbContext itself, only ever sees the read side (ITenantContext).
builder.Services.AddScoped<HttpRequestTenantContext>();
builder.Services.AddScoped<ITenantContext>(sp => sp.GetRequiredService<HttpRequestTenantContext>());
builder.Services.AddScoped<IMutableTenantContext>(sp => sp.GetRequiredService<HttpRequestTenantContext>());

// IDbServerProvider is the one seam AppDbContext's EF Core provider is
// configured through - lets integration tests swap in the InMemory
// provider via DI (Org.Service.Tests registers its own implementation)
// without a second, competing AddDbContext(UseInMemoryDatabase(...)) call
// in ConfigureTestServices, which would leave both providers' services
// registered in the same container and make EF Core's provider
// auto-detection throw. It also keeps the
// Microsoft.EntityFrameworkCore.InMemory package out of this project
// entirely - only the test project references it.
builder.Services.AddTransient<IDbServerProvider, NpgsqlDbServerProvider>();
builder.Services.AddDzabaHomeSecurityDataServices(
    (sp, options, connectionString) => sp.GetRequiredService<IDbServerProvider>().Configure(options, connectionString),
    sp => sp.GetRequiredService<IConfiguration>().GetConnectionString("AppDatabase")
        ?? throw new InvalidOperationException("Missing ConnectionStrings:AppDatabase"));

// Lazy-resolution delegate so services can defer their first AppDbContext
// access until after org creation has set the tenant - see the plan's
// "DbContext construction-order exception" note. Scoped AppDbContext means
// repeated invocations within one request return the same instance.
builder.Services.AddScoped<Func<AppDbContext>>(sp => sp.GetRequiredService<AppDbContext>);

// Fail open on a Redis outage (see PermissionEvaluator's source-loader
// fallback): tagged "degraded", not a hard readiness dependency, since a
// cache miss just costs one extra permission-source round-trip, not data
// loss (docs/architecture/14) - a Redis outage shouldn't pull the whole
// pod out of rotation the way a genuinely broken Postgres connection should.
builder.Services.AddDzabaHomeSecurityRedisCache(
    sp => sp.GetRequiredService<IConfiguration>().GetConnectionString("Redis")
        ?? throw new InvalidOperationException("Missing ConnectionStrings:Redis"),
    "degraded");

// Publishes access.changed (MembershipsService/UserRoleAssignmentsService) -
// a notification-only event with no consumer yet, published as a low-cost
// hedge for future integrations. See access_changed_message.json.
var rabbitMqConnectionString = builder.Configuration.GetConnectionString("RabbitMQ")
    ?? throw new InvalidOperationException("Missing ConnectionStrings:RabbitMQ");
builder.Services.AddDzabaHomeSecurityRabbitMqMessageBus(rabbitMqConnectionString);

builder.Services.AddTransient<IOrganizationsService, OrganizationsService>();
builder.Services.AddTransient<IMembershipsService, MembershipsService>();
builder.Services.AddTransient<IRolesService, RolesService>();
builder.Services.AddTransient<IUserRoleAssignmentsService, UserRoleAssignmentsService>();
builder.Services.AddTransient<IPermissionCatalogService, PermissionCatalogService>();
builder.Services.AddDzabaHomeSecurityHal();

builder.Services.AddHealthChecks()
    .AddNpgSql(sp => sp.GetRequiredService<IConfiguration>().GetConnectionString("AppDatabase")!, name: "postgres");

builder.Services.AddDzabaHomeSecurityOpenTelemetry(ServiceName,
    () => new Uri(builder.Configuration["OpenTelemetry:OtlpEndpoint"] ?? "http://localhost:4317"));

var app = builder.Build();

// Dev-only convenience: a single local instance can't race itself.
// Everywhere else, applying migrations is a deliberate, separate
// deployment step (e.g. a CI/CD migration job) - not something Program.cs
// does on every pod start, since concurrent Database.Migrate() calls
// across the >=2 replicas ADR-0011 requires would race on the same schema
// change. Also gated by config (default true), not just environment, so
// WebApplicationFactory-based integration tests - which default to
// "Development" - can opt out via Database:AutoMigrate=false when running
// against a non-relational provider (e.g. EF Core InMemory) that doesn't
// support Migrate() at all.
if (app.Environment.IsDevelopment() && builder.Configuration.GetValue("Database:AutoMigrate", true))
{
    // Constructed directly with StaticTenantContext, not resolved from DI:
    // there's no HTTP request during startup to have set the scoped
    // HttpRequestTenantContext, and migrations don't run through the
    // model's query filters anyway - same reasoning as AppDbContextFactory.
    using var scope = app.Services.CreateScope();
    var dbOptions = scope.ServiceProvider.GetRequiredService<DbContextOptions<AppDbContext>>();
    using var migrationContext = new AppDbContext(dbOptions, new StaticTenantContext(Guid.Empty));
    migrationContext.Database.Migrate();
}

app.UseSerilogRequestLogging();

// Defaults to true (secure by default per docs/README.md's HTTPS-only
// principle). Overridable via config/env var so environments that
// terminate TLS in front of this service - e.g. the local docker-compose
// stack, where nothing yet sits in front of it - can disable the redirect
// instead of 307-looping to a TLS port the container doesn't serve.
if (builder.Configuration.GetValue("Security:UseHttpsRedirection", true))
{
    app.UseHttpsRedirection();
}

app.UseRouting();
app.UseAuthentication();
app.UseMiddleware<TenantResolutionMiddleware>();
app.UseAuthorization();
app.MapControllers();

// Postgres and Redis checks are registered above via AddHealthChecks() -
// see MapDzabaHomeSecurityHealthChecks for the endpoint shape.
app.MapDzabaHomeSecurityHealthChecks();

app.UseDzabaHomeSecuritySwaggerUI("Org API", apiVersion);

app.Run();

/// <summary>Exposed as a partial class so <c>WebApplicationFactory&lt;Program&gt;</c> can reference it from integration tests.</summary>
public partial class Program;
