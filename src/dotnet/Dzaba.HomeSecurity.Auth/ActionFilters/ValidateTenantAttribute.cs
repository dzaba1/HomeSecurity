using Dzaba.HomeSecurity.Org.Contracts;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.DependencyInjection;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace Dzaba.HomeSecurity.Auth.ActionFilters;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
public sealed class ValidateTenantAttribute : ActionFilterAttribute
{
    public override async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var orgService = context.HttpContext.RequestServices.GetRequiredService<IOrgService>();
        var user = context.HttpContext.User;

        var tenantId = await orgService.GetTenantIdAsync(context.HttpContext).ConfigureAwait(false);
        if (string.IsNullOrEmpty(tenantId))
        {
            context.Result = new ForbidResult();
            return;
        }

        var userId = GetUserId(context.HttpContext);
        if (string.IsNullOrEmpty(userId))
        {
            context.Result = new ForbidResult();
            return;
        }

        var hasAccess = await orgService.HasAccessAsync(userId, tenantId).ConfigureAwait(false);
        if (!hasAccess)
        {
            context.Result = new ForbidResult();
            return;
        }

        await next().ConfigureAwait(false);
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
