using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Dzaba.HomeSecurity.DeviceAuth;

public static class Bootstrapper
{
    /// <summary>
    /// Registers an authorization policy named after <paramref name="scope"/>
    /// that requires the device JWT's "scope" claim to contain it (e.g.
    /// "logs:write", "router:read"), plus the handler that checks it.
    /// Restrict a policy to the device-token scheme, where a service also
    /// authenticates humans, with
    /// <c>[Authorize(AuthenticationSchemes = ..., Policy = scope)]</c>.
    /// </summary>
    public static IServiceCollection AddDzabaHomeSecurityDeviceScopePolicy(this IServiceCollection services, string scope)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrEmpty(scope);

        services.AddAuthorizationBuilder()
            .AddPolicy(scope, policy => policy.Requirements.Add(new ScopeRequirement(scope)));
        services.TryAddEnumerable(ServiceDescriptor.Transient<IAuthorizationHandler, ScopeAuthorizationHandler>());

        return services;
    }
}
