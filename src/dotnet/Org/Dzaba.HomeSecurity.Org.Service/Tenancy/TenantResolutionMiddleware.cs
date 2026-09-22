using Dzaba.AspNetUtils;
using Dzaba.HomeSecurity.Data;
using Dzaba.HomeSecurity.Domain;
using Microsoft.EntityFrameworkCore;
using Serilog.Context;

namespace Dzaba.HomeSecurity.Org.Service.Tenancy;

/// <summary>
/// The single, centralized place a request's {orgId} route segment is
/// verified against the caller's actual <c>Membership</c> before being
/// trusted as the request's tenant - replacing the duplicated
/// middleware+action-filter split from the old prototype. Must run after
/// UseAuthentication() and before UseAuthorization()/MapControllers(), so
/// every AppDbContext constructed later in the request sees the resolved
/// tenant - see docs/architecture/02-multi-tenancy.md. Also pushes
/// tenant_id/user_id into the Serilog LogContext, pulled from the
/// authenticated identity, never a client-supplied value - see
/// docs/architecture/09-observability.md.
/// </summary>
internal sealed class TenantResolutionMiddleware
{
    private readonly RequestDelegate next;

    public TenantResolutionMiddleware(RequestDelegate next)
    {
        ArgumentNullException.ThrowIfNull(next);

        this.next = next;
    }

    public async Task InvokeAsync(HttpContext context, IMutableTenantContext tenantContext,
        DbContextOptions<AppDbContext> dbOptions)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(tenantContext);
        ArgumentNullException.ThrowIfNull(dbOptions);

        var userId = context.User.Identity?.IsAuthenticated == true ? context.User.GetUserSubOrNameIdentifier() : null;
        using var userIdScope = string.IsNullOrEmpty(userId) ? null : LogContext.PushProperty("user_id", userId);

        if (!context.Request.RouteValues.TryGetValue("orgId", out var rawOrgId))
        {
            // Not an org-scoped route (e.g. GET /api/v1/orgs, GET
            // /api/v1/permissions). AppDbContext's constructor always reads
            // ITenantContext.TenantId regardless of whether the query it
            // will run is even tenant-filtered, so it must still be set to
            // *something* here - Guid.Empty is safe because every table
            // these routes touch is either unfiltered (Permission,
            // Organization) or explicitly bypasses the filter
            // (IgnoreQueryFilters(), for "list my orgs").
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

        // A throwaway AppDbContext bound to the claimed org - the request's
        // real (ambient-tenant) AppDbContext can't be used here, because the
        // ambient tenant hasn't been resolved yet (that's what this check
        // decides). Same escape-hatch pattern as DataTestFixture.CreateContext.
        using (var checkDb = new AppDbContext(dbOptions, new StaticTenantContext(orgId)))
        {
            var isMember = await checkDb.Memberships
                .AnyAsync(m => m.OrganizationId == orgId && m.UserId == userId)
                .ConfigureAwait(false);

            if (!isMember)
            {
                // 404, not 403: an authenticated caller who isn't a member
                // gets the same response as a nonexistent org, preventing
                // org-existence/membership enumeration.
                context.Response.StatusCode = StatusCodes.Status404NotFound;
                return;
            }
        }

        tenantContext.SetTenantId(orgId);
        using var tenantIdScope = LogContext.PushProperty("tenant_id", orgId);
        await next(context).ConfigureAwait(false);
    }
}
