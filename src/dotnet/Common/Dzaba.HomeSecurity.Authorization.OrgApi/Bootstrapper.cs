using Microsoft.Extensions.DependencyInjection;

namespace Dzaba.HomeSecurity.Authorization.OrgApi;

public static class Bootstrapper
{
    /// <summary>
    /// Registers the Org.Service-API-backed <see cref="IPermissionSourceLoader"/>
    /// for a service that doesn't own Membership/UserRole/RolePermission.
    /// <paramref name="addressProvider"/> resolves Org.Service's base address
    /// from the built container (e.g. from configuration), when the client is
    /// first created. Needs IHttpContextAccessor (AddHttpContextAccessor) to
    /// relay the caller's bearer token. The client uses the standard
    /// resilience handler (retry, circuit breaker, timeouts - Polly under the
    /// hood) rather than hand-rolled retry logic; a non-transient failure
    /// still surfaces as an exception, never a silent "no permissions".
    /// </summary>
    public static IServiceCollection AddDzabaHomeSecurityOrgApiPermissionSource(this IServiceCollection services,
        Func<IServiceProvider, Uri> addressProvider)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(addressProvider);

        services.AddHttpClient<IPermissionSourceLoader, OrgApiPermissionSourceLoader>((sp, client) =>
                client.BaseAddress = addressProvider(sp))
            .AddStandardResilienceHandler();

        return services;
    }
}
