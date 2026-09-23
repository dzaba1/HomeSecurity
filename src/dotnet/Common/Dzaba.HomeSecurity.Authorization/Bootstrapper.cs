using Dzaba.HomeSecurity.Domain;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Dzaba.HomeSecurity.Authorization;

/// <summary>
/// Registers the permission-catalog policies (one per
/// <see cref="PermissionKeys.All"/> entry), the authorization handler that
/// evaluates them, and the access-context cache they share. Callers still
/// register their own <see cref="IPermissionSourceLoader"/> implementation
/// (Org.Service's queries its own tables directly; a service that doesn't
/// own that data calls Org.Service's access-context API instead) - this
/// method doesn't know or care which.
/// </summary>
public static class Bootstrapper
{
    public static IServiceCollection AddDzabaHomeSecurityPermissionAuthorization(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.Configure<PermissionCacheOptions>(configuration.GetSection(PermissionCacheOptions.SectionName));

        var authorizationBuilder = services.AddAuthorizationBuilder();
        foreach (var permissionKey in PermissionKeys.All)
        {
            authorizationBuilder.AddPolicy(permissionKey, policy => policy.Requirements.Add(new PermissionRequirement(permissionKey)));
        }

        services.AddTransient<IAuthorizationHandler, PermissionAuthorizationHandler>();
        services.AddTransient<IPermissionEvaluator, PermissionEvaluator>();

        return services;
    }

    /// <summary>
    /// Registers the per-request tenant context: one scoped
    /// <see cref="MutableTenantContext"/> behind both
    /// <see cref="ITenantContext"/> (what tenant-scoped DbContexts and
    /// application code see) and <see cref="IMutableTenantContext"/> (only
    /// <see cref="TenantResolutionMiddleware"/> and org creation write it).
    /// Scoped, not transient, since it holds real per-request state.
    /// </summary>
    public static IServiceCollection AddDzabaHomeSecurityTenancy(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddScoped<MutableTenantContext>();
        services.AddScoped<ITenantContext>(sp => sp.GetRequiredService<MutableTenantContext>());
        services.AddScoped<IMutableTenantContext>(sp => sp.GetRequiredService<MutableTenantContext>());

        return services;
    }

    /// <summary>
    /// For a service that doesn't own Membership directly: answers
    /// <see cref="TenantResolutionMiddleware"/>'s membership check from the
    /// shared access-context cache, via <see cref="IPermissionEvaluator"/>.
    /// A service that does own it registers its own
    /// <see cref="ITenantMembershipChecker"/> instead.
    /// </summary>
    public static IServiceCollection AddDzabaHomeSecurityCachedTenantMembership(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddTransient<ITenantMembershipChecker, PermissionEvaluatorTenantMembershipChecker>();

        return services;
    }
}
