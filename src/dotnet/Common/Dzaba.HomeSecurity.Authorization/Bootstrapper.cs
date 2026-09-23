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
}
