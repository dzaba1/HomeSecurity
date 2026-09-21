using Dzaba.HomeSecurity.Org.Contracts;
using Microsoft.AspNetCore.Http;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace Dzaba.HomeSecurity.Auth;

public sealed class TenantAccessMiddleware
{
    private readonly RequestDelegate _next;

    public TenantAccessMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task Invoke(HttpContext context, IOrgService orgService)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(orgService);

        var tenantId = await orgService.GetTenantIdAsync(context).ConfigureAwait(false);
        var userId = GetUserId(context);

        if (tenantId != null && userId != null)
        {
            var hasAccess = await orgService.HasAccessAsync(userId, tenantId).ConfigureAwait(false);

            if (!hasAccess)
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                return;
            }
        }

        if (_next != null)
        {
            await _next(context).ConfigureAwait(false);
        }
    }

    private string GetUserId(HttpContext context)
    {
        var bySub = context.User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
        if (!string.IsNullOrEmpty(bySub))
        {
            return bySub;
        }
        return context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
    }
}
