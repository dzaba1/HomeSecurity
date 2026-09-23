using Dzaba.AspNetUtils;
using Dzaba.HomeSecurity.Domain;
using Microsoft.AspNetCore.Http;
using Serilog.Context;

namespace Dzaba.HomeSecurity.Authorization;

/// <summary>
/// The single, centralized place a request's {orgId} route segment is
/// verified against the caller's actual membership before being trusted as
/// the request's tenant. Must run after UseAuthentication() and before
/// UseAuthorization()/MapControllers(), so every tenant-scoped DbContext
/// constructed later in the request sees the resolved tenant - see
/// docs/architecture/02-multi-tenancy.md. Also pushes tenant_id/user_id
/// into the Serilog LogContext, pulled from the authenticated identity,
/// never a client-supplied value - see docs/architecture/09-observability.md.
/// How membership is actually determined is the service's own
/// <see cref="ITenantMembershipChecker"/>.
/// </summary>
public sealed class TenantResolutionMiddleware
{
    private readonly RequestDelegate next;

    public TenantResolutionMiddleware(RequestDelegate next)
    {
        ArgumentNullException.ThrowIfNull(next);

        this.next = next;
    }

    public async Task InvokeAsync(HttpContext context, IMutableTenantContext tenantContext,
        ITenantMembershipChecker membershipChecker)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(tenantContext);
        ArgumentNullException.ThrowIfNull(membershipChecker);

        var userId = context.User.Identity?.IsAuthenticated == true ? context.User.GetUserSubOrNameIdentifier() : null;
        using var userIdScope = string.IsNullOrEmpty(userId) ? null : LogContext.PushProperty("user_id", userId);

        if (!context.Request.RouteValues.TryGetValue("orgId", out var rawOrgId))
        {
            // Not an org-scoped route (e.g. GET /api/v1/orgs, GET
            // /api/v1/permissions). A tenant-scoped DbContext's constructor
            // always reads ITenantContext.TenantId regardless of whether the
            // query it will run is even tenant-filtered, so it must still be
            // set to *something* here - Guid.Empty is safe because every
            // table such a route touches is either unfiltered or explicitly
            // bypasses the filter.
            tenantContext.SetTenantId(Guid.Empty);
            await next(context).ConfigureAwait(false);
            return;
        }

        if (context.User.Identity?.IsAuthenticated != true)
        {
            // Defensive: UseAuthentication() already ran by this point.
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return;
        }

        if (!Guid.TryParse(rawOrgId?.ToString(), out var orgId))
        {
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            return;
        }

        if (string.IsNullOrEmpty(userId))
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return;
        }

        var isMember = await membershipChecker.IsMemberAsync(orgId, userId, context.RequestAborted).ConfigureAwait(false);
        if (!isMember)
        {
            // 404, not 403: an authenticated caller who isn't a member gets
            // the same response as a nonexistent org, preventing
            // org-existence/membership enumeration.
            context.Response.StatusCode = StatusCodes.Status404NotFound;
            return;
        }

        tenantContext.SetTenantId(orgId);
        using var tenantIdScope = LogContext.PushProperty("tenant_id", orgId);
        await next(context).ConfigureAwait(false);
    }
}
