using Asp.Versioning;
using Dzaba.AspNetUtils.ActionFilters;
using Dzaba.HomeSecurity.DeviceAuth;
using Dzaba.HomeSecurity.Devices.Service.Authorization;
using Dzaba.HomeSecurity.Devices.Service.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Dzaba.HomeSecurity.Devices.Service.Controllers;

/// <summary>
/// The one endpoint the agent app calls, per docs/architecture/15-router-credentials.md:
/// it fetches the connection settings, decrypted secret included, of the router
/// it is paired with. Authenticated with a device token (scope router:read),
/// not a human's; tenant and device come from that token alone. Plain JSON, no
/// HAL - see docs/decisions/0013-hateoas-for-org-api.md.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Authorize(AuthenticationSchemes = AgentAuth.DeviceTokenScheme, Policy = AgentAuth.RouterReadScope)]
[HandleErrors]
[Route("api/v{version:apiVersion}/devices/{deviceId:guid}/router-config")]
public sealed class AgentRouterConfigController : ControllerBase
{
    private readonly IAgentRouterConfigService routerConfigs;

    public AgentRouterConfigController(IAgentRouterConfigService routerConfigs)
    {
        ArgumentNullException.ThrowIfNull(routerConfigs);

        this.routerConfigs = routerConfigs;
    }

    // The response carries a plaintext secret, so no cache or proxy may keep it.
    [HttpGet]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<IActionResult> Get(Guid deviceId, CancellationToken cancellationToken)
    {
        // An agent may only read the config of the credential its own token
        // was issued to; the route's deviceId is a cross-check, not an input.
        if (deviceId != User.GetDeviceId())
        {
            return Forbid(AgentAuth.DeviceTokenScheme);
        }

        var config = await routerConfigs.GetAsync(User.GetTenantId(), deviceId, cancellationToken).ConfigureAwait(false);
        if (config is null)
        {
            return NotFound();
        }

        return Ok(config);
    }
}
