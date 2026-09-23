using Dzaba.AspNetUtils;
using Dzaba.HomeSecurity.Domain;
using Microsoft.AspNetCore.Authorization;

namespace Dzaba.HomeSecurity.Authorization;

/// <summary>
/// Evaluates one <see cref="PermissionRequirement"/> against the resolved
/// ambient tenant. Relies on <see cref="ITenantContext"/> already being set
/// by the consuming service's own tenant-resolution middleware, which must
/// run before UseAuthorization().
/// </summary>
internal sealed class PermissionAuthorizationHandler : AuthorizationHandler<PermissionRequirement>
{
    private readonly ITenantContext tenantContext;
    private readonly IPermissionEvaluator permissionEvaluator;

    public PermissionAuthorizationHandler(ITenantContext tenantContext, IPermissionEvaluator permissionEvaluator)
    {
        ArgumentNullException.ThrowIfNull(tenantContext);
        ArgumentNullException.ThrowIfNull(permissionEvaluator);

        this.tenantContext = tenantContext;
        this.permissionEvaluator = permissionEvaluator;
    }

    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context, PermissionRequirement requirement)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(requirement);

        var userId = context.User.GetUserSubOrNameIdentifier();
        if (string.IsNullOrEmpty(userId))
        {
            return;
        }

        var hasPermission = await permissionEvaluator
            .HasPermissionAsync(userId, tenantContext.TenantId, requirement.PermissionKey, CancellationToken.None)
            .ConfigureAwait(false);

        if (hasPermission)
        {
            context.Succeed(requirement);
        }
    }
}
