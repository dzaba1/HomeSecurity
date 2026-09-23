using Dzaba.HomeSecurity.WebApi.Hal;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.OpenApi;

namespace Dzaba.HomeSecurity.WebApi;

public static class Bootstrapper
{
    /// <summary>
    /// Registers <see cref="ILinkFactory"/> for building HAL <c>_links</c>
    /// hrefs via ASP.NET Core's own LinkGenerator - see Hal/HalEnvelope.cs.
    /// </summary>
    public static IServiceCollection AddDzabaHomeSecurityHal(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddTransient<ILinkFactory, LinkFactory>();

        return services;
    }

    /// <summary>
    /// URI path versioning shared by every service, per ADR-0012: explicit
    /// UrlSegmentApiVersionReader rather than the reflection-based default
    /// reader (per the analyzer's own performance guidance, AV0015), no
    /// implicit-fallback version, and Swagger group names substituted as
    /// /v{n} in the route.
    /// </summary>
    public static IServiceCollection AddDzabaHomeSecurityApiVersioning(this IServiceCollection services, Version defaultApiVersion)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(defaultApiVersion);

        services.AddApiVersioning(options =>
        {
            options.DefaultApiVersion = new Asp.Versioning.ApiVersion(defaultApiVersion.Major, defaultApiVersion.Minor);
            options.ApiVersionReader = new Asp.Versioning.UrlSegmentApiVersionReader();
            options.AssumeDefaultVersionWhenUnspecified = false;
            options.ReportApiVersions = true;
        })
            .AddMvc()
            .AddApiExplorer(options =>
            {
                options.GroupNameFormat = "'v'VVV";
                options.SubstituteApiVersionInUrl = true;
            });

        return services;
    }

    /// <summary>
    /// Liveness ("/health/live": process is up, no dependency checks) and
    /// readiness ("/health/ready": every registered health check not tagged
    /// "degraded") endpoints shared by every service. A service tags a check
    /// "degraded" when it has its own fallback for that dependency being down
    /// (e.g. Org.Service's Redis check - PermissionEvaluator fails open to
    /// Postgres), so that dependency's outage shouldn't pull the whole pod
    /// out of rotation. Both endpoints are unauthenticated but only ever
    /// reachable from inside the cluster - see docs/architecture/09-observability.md.
    /// </summary>
    public static IEndpointRouteBuilder MapDzabaHomeSecurityHealthChecks(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        endpoints.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false });
        endpoints.MapHealthChecks("/health/ready", new HealthCheckOptions
        {
            Predicate = check => !check.Tags.Contains("degraded"),
        });

        return endpoints;
    }

    /// <summary>
    /// Swagger generation shared by every service: a single "v{major}"
    /// document named after <paramref name="version"/>'s major component,
    /// matching the URI path versioning's group name format.
    /// </summary>
    public static IServiceCollection AddDzabaHomeSecuritySwaggerGen(this IServiceCollection services, string title, Version version)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        ArgumentNullException.ThrowIfNull(version);

        var versionString = ToSwaggerVersionString(version);

        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen(options =>
        {
            options.SwaggerDoc(versionString, new OpenApiInfo { Title = title, Version = versionString });
        });

        return services;
    }

    /// <summary>
    /// Swagger UI shared by every service, serving the single "v{major}"
    /// document <see cref="AddDzabaHomeSecuritySwaggerGen"/> registered.
    /// Gated by Swagger:Enabled config (default: only in Development) rather
    /// than Debug/Release build configuration - a Release build is what
    /// actually deploys to Staging too, and #if DEBUG would make Swagger
    /// unreachable there even when wanted.
    /// </summary>
    public static WebApplication UseDzabaHomeSecuritySwaggerUI(this WebApplication app, string title, Version version)
    {
        ArgumentNullException.ThrowIfNull(app);
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        ArgumentNullException.ThrowIfNull(version);

        var swaggerEnabled = app.Configuration.GetValue("Swagger:Enabled", app.Environment.IsDevelopment());
        if (swaggerEnabled)
        {
            var versionString = ToSwaggerVersionString(version);

            app.UseSwagger();
            app.UseSwaggerUI(options => options.SwaggerEndpoint($"/swagger/{versionString}/swagger.json", $"{title} {versionString}"));
        }

        return app;
    }

    private static string ToSwaggerVersionString(Version version) => $"v{version.Major}";
}
