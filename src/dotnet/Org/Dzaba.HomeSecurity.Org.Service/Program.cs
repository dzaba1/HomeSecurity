using Dzaba.HomeSecurity.Data;
using Dzaba.HomeSecurity.Domain;
using Dzaba.HomeSecurity.Org.Service.Authorization;
using Dzaba.HomeSecurity.Org.Service.Hal;
using Dzaba.HomeSecurity.Org.Service.Logging;
using Dzaba.HomeSecurity.Org.Service.Services;
using Dzaba.HomeSecurity.Org.Service.Tenancy;
using Dzaba.Utils.AspNet;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Serilog;
using Serilog.Formatting.Json;
using StackExchange.Redis;

const string ServiceName = "Dzaba.HomeSecurity.Org.Service";

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((context, services, configuration) => configuration
    .Enrich.FromLogContext()
    .Enrich.WithThreadId()
    .Enrich.With<ActivityEnricher>()
    .Enrich.WithProperty("service_name", ServiceName)
    .Enrich.WithProperty("environment", context.HostingEnvironment.EnvironmentName)
    // Console only, structured JSON - shipping (e.g. to Loki) is a
    // cluster-infra concern, not something this service configures for
    // itself. Never destructure request/command objects here - device
    // secrets/JWTs/Keycloak credentials must never be logged, per
    // docs/architecture/09-observability.md.
    .WriteTo.Console(new JsonFormatter()));

builder.Services.AddHttpContextAccessor();
builder.Services.AddControllers();

builder.Services.AddApiVersioning(options =>
{
    options.DefaultApiVersion = new Asp.Versioning.ApiVersion(1, 0);
    // URI path versioning per ADR-0012 - explicit rather than the reflection-based
    // default reader, per the analyzer's own performance guidance (AV0015).
    options.ApiVersionReader = new Asp.Versioning.UrlSegmentApiVersionReader();
    // Every route must carry /v{n} explicitly - matches ADR-0012's
    // rejection of an implicit-fallback version.
    options.AssumeDefaultVersionWhenUnspecified = false;
    options.ReportApiVersions = true;
})
    .AddMvc()
    .AddApiExplorer(options =>
    {
        options.GroupNameFormat = "'v'VVV";
        options.SubstituteApiVersionInUrl = true;
    });

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo { Title = "Org API", Version = "v1" });
});

builder.Services.AddJwtAuthentication(() => new JwtSettings
{
    Authority = builder.Configuration["Authentication:Keycloak:Authority"],
    Audience = builder.Configuration["Authentication:Keycloak:Audience"],
    RequireHttps = builder.Environment.IsProduction(),
    ValidateAudience = true,
    ValidateIssuer = true,
});

var authorizationBuilder = builder.Services.AddAuthorizationBuilder();
foreach (var permissionKey in PermissionKeys.All)
{
    authorizationBuilder.AddPolicy(permissionKey, policy => policy.Requirements.Add(new PermissionRequirement(permissionKey)));
}
builder.Services.AddScoped<IAuthorizationHandler, PermissionAuthorizationHandler>();

// One scoped HttpRequestTenantContext instance behind both interfaces - the
// write side (IMutableTenantContext) is used only by
// TenantResolutionMiddleware and org creation; everything else, including
// AppDbContext itself, only ever sees the read side (ITenantContext).
builder.Services.AddScoped<HttpRequestTenantContext>();
builder.Services.AddScoped<ITenantContext>(sp => sp.GetRequiredService<HttpRequestTenantContext>());
builder.Services.AddScoped<IMutableTenantContext>(sp => sp.GetRequiredService<HttpRequestTenantContext>());

builder.Services.AddDzabaHomeSecurityDataServices(
    (_, options, connectionString) => options.UseNpgsql(connectionString),
    sp => sp.GetRequiredService<IConfiguration>().GetConnectionString("AppDatabase")
        ?? throw new InvalidOperationException("Missing ConnectionStrings:AppDatabase"));

// Lazy-resolution delegate so services can defer their first AppDbContext
// access until after org creation has set the tenant - see the plan's
// "DbContext construction-order exception" note. Scoped AppDbContext means
// repeated invocations within one request return the same instance.
builder.Services.AddScoped<Func<AppDbContext>>(sp => sp.GetRequiredService<AppDbContext>);

builder.Services.AddSingleton<IConnectionMultiplexer>(sp =>
{
    var connectionString = sp.GetRequiredService<IConfiguration>().GetConnectionString("Redis")
        ?? throw new InvalidOperationException("Missing ConnectionStrings:Redis");
    return ConnectionMultiplexer.Connect(connectionString);
});
builder.Services.Configure<PermissionCacheOptions>(builder.Configuration.GetSection(PermissionCacheOptions.SectionName));

builder.Services.AddScoped<IPermissionEvaluator, PermissionEvaluator>();
builder.Services.AddScoped<IOrganizationsService, OrganizationsService>();
builder.Services.AddScoped<IMembershipsService, MembershipsService>();
builder.Services.AddScoped<IRolesService, RolesService>();
builder.Services.AddScoped<IUserRoleAssignmentsService, UserRoleAssignmentsService>();
builder.Services.AddScoped<IPermissionCatalogService, PermissionCatalogService>();
builder.Services.AddScoped<IOrgLinkFactory, OrgLinkFactory>();

builder.Services.AddHealthChecks()
    .AddNpgSql(sp => sp.GetRequiredService<IConfiguration>().GetConnectionString("AppDatabase")!, name: "postgres")
    .AddRedis(sp => sp.GetRequiredService<IConnectionMultiplexer>(), name: "redis");

builder.Services.AddOpenTelemetry()
    .ConfigureResource(resource => resource.AddService(ServiceName))
    .WithMetrics(metrics => metrics
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation()
        .AddRuntimeInstrumentation()
        .AddOtlpExporter(otlp => otlp.Endpoint = new Uri(builder.Configuration["OpenTelemetry:OtlpEndpoint"] ?? "http://localhost:4317")))
    .WithTracing(tracing => tracing
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation()
        .AddOtlpExporter(otlp => otlp.Endpoint = new Uri(builder.Configuration["OpenTelemetry:OtlpEndpoint"] ?? "http://localhost:4317")));

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    // Dev-only convenience: a single local instance can't race itself.
    // Everywhere else, applying migrations is a deliberate, separate
    // deployment step (e.g. a CI/CD migration job) - not something
    // Program.cs does on every pod start, since concurrent Database.Migrate()
    // calls across the >=2 replicas ADR-0011 requires would race on the
    // same schema change.
    //
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
app.UseHttpsRedirection();
app.UseRouting();
app.UseAuthentication();
app.UseMiddleware<TenantResolutionMiddleware>();
app.UseAuthorization();
app.MapControllers();

// Liveness: process is up, no dependency checks. Readiness: every
// registered check (Postgres, Redis). Both unauthenticated but only ever
// reachable from inside the cluster - see docs/architecture/09-observability.md.
app.MapHealthChecks("/health/live", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions { Predicate = _ => false });
app.MapHealthChecks("/health/ready");

// Gate by runtime environment/config, not Debug/Release build
// configuration - a Release build is what actually deploys to Staging too,
// and #if DEBUG would make Swagger unreachable there even when wanted.
var swaggerEnabled = builder.Configuration.GetValue("Swagger:Enabled", app.Environment.IsDevelopment());
if (swaggerEnabled)
{
    app.UseSwagger();
    app.UseSwaggerUI(options => options.SwaggerEndpoint("/swagger/v1/swagger.json", "Org API v1"));
}

app.Run();

/// <summary>Exposed as a partial class so <c>WebApplicationFactory&lt;Program&gt;</c> can reference it from integration tests.</summary>
public partial class Program;
