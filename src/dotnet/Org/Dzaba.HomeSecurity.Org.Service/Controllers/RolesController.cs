using Asp.Versioning;
using Dzaba.AspNetUtils;
using Dzaba.AspNetUtils.ActionFilters;
using Dzaba.HomeSecurity.Domain;
using Dzaba.HomeSecurity.Authorization;
using Dzaba.HomeSecurity.Org.Contracts;
using Dzaba.HomeSecurity.Org.Service.Services;
using Dzaba.HomeSecurity.WebApi.Hal;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace Dzaba.HomeSecurity.Org.Service.Controllers;

/// <summary>
/// List/Get are "membership only"; Create/Update/Delete require
/// org.manage_members. Update/Delete additionally 403 on a system role
/// (TenantId null) - enforced in RolesService, not here.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Authorize]
[ValidateModel]
[HandleErrors]
[Route("api/v{version:apiVersion}/orgs/{orgId:guid}/roles")]
public sealed class RolesController : HalControllerBase
{
    private readonly IRolesService roles;
    private readonly IPermissionEvaluator permissionEvaluator;

    public RolesController(IRolesService roles, IPermissionEvaluator permissionEvaluator,
        ILinkFactory links, IOptions<JsonOptions> jsonOptions)
        : base(links, jsonOptions)
    {
        ArgumentNullException.ThrowIfNull(roles);
        ArgumentNullException.ThrowIfNull(permissionEvaluator);

        this.roles = roles;
        this.permissionEvaluator = permissionEvaluator;
    }

    [HttpGet]
    public async Task<IActionResult> List(Guid orgId, CancellationToken cancellationToken)
    {
        var items = new List<Role>();
        await foreach (var role in roles.ListAsync(cancellationToken))
        {
            items.Add(role);
        }

        var links = new Dictionary<string, HalLink>
        {
            ["self"] = Links.Build(HttpContext, nameof(List), "Roles", new { orgId }),
            ["organization"] = Links.Build(HttpContext, nameof(OrgsController.Get), "Orgs", new { orgId }),
        };

        var userId = User.GetUserSubOrNameIdentifier();
        if (await permissionEvaluator.HasPermissionAsync(userId, orgId, PermissionKeys.OrgManageMembers, cancellationToken).ConfigureAwait(false))
        {
            links["create"] = Links.Build(HttpContext, nameof(Create), "Roles", new { orgId }, "POST");
        }

        var body = HalEnvelope.WrapCollection("roles", items, links, JsonOptions);
        return Ok(body);
    }

    [HttpGet("{roleId:guid}")]
    public async Task<IActionResult> Get(Guid orgId, Guid roleId, CancellationToken cancellationToken)
    {
        var role = await roles.GetAsync(roleId, cancellationToken).ConfigureAwait(false);
        if (role is null)
        {
            return NotFound();
        }

        var links = new Dictionary<string, HalLink>
        {
            ["self"] = Links.Build(HttpContext, nameof(Get), "Roles", new { orgId, roleId }),
            ["organization"] = Links.Build(HttpContext, nameof(OrgsController.Get), "Orgs", new { orgId }),
        };

        var userId = User.GetUserSubOrNameIdentifier();
        var canManage = role.TenantId is not null &&
            await permissionEvaluator.HasPermissionAsync(userId, orgId, PermissionKeys.OrgManageMembers, cancellationToken).ConfigureAwait(false);
        if (canManage)
        {
            links["edit"] = Links.Build(HttpContext, nameof(Update), "Roles", new { orgId, roleId }, "PATCH");
            links["delete"] = Links.Build(HttpContext, nameof(Delete), "Roles", new { orgId, roleId }, "DELETE");
        }

        var body = HalEnvelope.Wrap(role, links, JsonOptions);
        return Ok(body);
    }

    [HttpPost]
    [Authorize(Policy = PermissionKeys.OrgManageMembers)]
    public async Task<IActionResult> Create(Guid orgId, CreateRole request, CancellationToken cancellationToken)
    {
        var role = await roles.CreateAsync(request, cancellationToken).ConfigureAwait(false);

        var links = new Dictionary<string, HalLink>
        {
            ["self"] = Links.Build(HttpContext, nameof(Get), "Roles", new { orgId, roleId = role.Id }),
            ["organization"] = Links.Build(HttpContext, nameof(OrgsController.Get), "Orgs", new { orgId }),
        };

        var body = HalEnvelope.Wrap(role, links, JsonOptions);
        return CreatedAtAction(nameof(Get), new { orgId, roleId = role.Id }, body);
    }

    [HttpPatch("{roleId:guid}")]
    [Authorize(Policy = PermissionKeys.OrgManageMembers)]
    public async Task<IActionResult> Update(Guid orgId, Guid roleId, CreateRole request, CancellationToken cancellationToken)
    {
        var role = await roles.UpdateAsync(roleId, request, cancellationToken).ConfigureAwait(false);
        if (role is null)
        {
            return NotFound();
        }

        var links = new Dictionary<string, HalLink> { ["self"] = Links.Build(HttpContext, nameof(Get), "Roles", new { orgId, roleId }) };
        var body = HalEnvelope.Wrap(role, links, JsonOptions);
        return Ok(body);
    }

    [HttpDelete("{roleId:guid}")]
    [Authorize(Policy = PermissionKeys.OrgManageMembers)]
    public async Task<IActionResult> Delete(Guid orgId, Guid roleId, CancellationToken cancellationToken)
    {
        var deleted = await roles.DeleteAsync(roleId, cancellationToken).ConfigureAwait(false);
        return deleted ? NoContent() : NotFound();
    }
}
