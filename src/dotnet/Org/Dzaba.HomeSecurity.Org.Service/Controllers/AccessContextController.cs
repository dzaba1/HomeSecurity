using Asp.Versioning;
using Dzaba.AspNetUtils;
using Dzaba.AspNetUtils.ActionFilters;
using Dzaba.HomeSecurity.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Dzaba.HomeSecurity.Org.Service.Controllers;

/// <summary>
/// Service-to-service integration point: a caller that doesn't own
/// Membership/UserRole/RolePermission directly (e.g. a future
/// Devices.Service) resolves the token bearer's effective permissions
/// here, by forwarding the same bearer token it was itself called with
/// ("token relay") - no new service-account auth scheme needed.
/// Deliberately outside the HAL/_links surface (ADR-0013's carve-out):
/// this isn't a resource a browser client navigates to.
///
/// TenantResolutionMiddleware already 404s a non-member before this
/// action ever runs, so a 200 response always means the caller is a
/// member - the body only needs to carry which permissions they hold.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Authorize]
[HandleErrors]
[Route("api/v{version:apiVersion}/orgs/{orgId:guid}/access-context")]
public sealed class AccessContextController : ControllerBase
{
    private readonly IPermissionSourceLoader sourceLoader;

    public AccessContextController(IPermissionSourceLoader sourceLoader)
    {
        ArgumentNullException.ThrowIfNull(sourceLoader);

        this.sourceLoader = sourceLoader;
    }

    [HttpGet]
    public async Task<IActionResult> Get(Guid orgId, CancellationToken cancellationToken)
    {
        var userId = User.GetUserSubOrNameIdentifier();
        var accessContext = await sourceLoader.LoadAsync(orgId, userId, cancellationToken).ConfigureAwait(false);

        return Ok(new { permissionKeys = accessContext.PermissionKeys });
    }
}
