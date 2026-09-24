using Dzaba.HomeSecurity.Devices.Contracts;

namespace Dzaba.HomeSecurity.Devices.Service.Services;

public interface IAgentRouterConfigService
{
    /// <summary>
    /// The connection settings, including the decrypted secret, of the router
    /// the given agent credential is paired with; null when that credential
    /// has no pairing in the tenant. The tenant comes from the caller's
    /// validated device token, never from the request.
    /// </summary>
    Task<RouterConfig?> GetAsync(Guid tenantId, Guid deviceCredentialId, CancellationToken cancellationToken);
}
