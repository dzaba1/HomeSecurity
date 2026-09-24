using Microsoft.AspNetCore.Authorization;

namespace Dzaba.HomeSecurity.DeviceAuth;

internal sealed class ScopeAuthorizationHandler : AuthorizationHandler<ScopeRequirement>
{
    protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, ScopeRequirement requirement)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(requirement);

        var scopeClaim = context.User.FindFirst(DeviceClaimTypes.Scope)?.Value;
        if (!string.IsNullOrEmpty(scopeClaim))
        {
            var scopes = scopeClaim.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (scopes.Contains(requirement.RequiredScope, StringComparer.Ordinal))
            {
                context.Succeed(requirement);
            }
        }

        return Task.CompletedTask;
    }
}
