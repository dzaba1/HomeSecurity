using Asp.Versioning;
using Dzaba.AspNetUtils.ActionFilters;
using Dzaba.HomeSecurity.Domain;
using Dzaba.HomeSecurity.Org.Contracts;
using Dzaba.HomeSecurity.Org.Service.Services;
using Dzaba.HomeSecurity.WebApi.Hal;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace Dzaba.HomeSecurity.Org.Service.Controllers;

/// <summary>
/// Every action here requires org.manage_members - unlike Members/Roles,
/// List is not membership-only for this resource, per the endpoint table.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Authorize(Policy = PermissionKeys.OrgManageMembers)]
[ValidateModel]
[HandleErrors]
[Route("api/v{version:apiVersion}/orgs/{orgId:guid}/user-roles")]
public sealed class UserRolesController : OrgControllerBase
{
    private readonly IUserRoleAssignmentsService userRoles;

    public UserRolesController(IUserRoleAssignmentsService userRoles, ILinkFactory links, IOptions<JsonOptions> jsonOptions)
        : base(links, jsonOptions)
    {
        ArgumentNullException.ThrowIfNull(userRoles);

        this.userRoles = userRoles;
    }

    [HttpGet]
    public async Task<IActionResult> List(Guid orgId, CancellationToken cancellationToken)
    {
        var items = new List<UserRoleAssignment>();
        await foreach (var assignment in userRoles.ListAsync(cancellationToken))
        {
            items.Add(assignment);
        }

        var links = new Dictionary<string, HalLink>
        {
            ["self"] = Links.Build(HttpContext, nameof(List), "UserRoles", new { orgId }),
            ["organization"] = Links.Build(HttpContext, nameof(OrgsController.Get), "Orgs", new { orgId }),
        };

        var body = HalEnvelope.WrapCollection("userRoles", items, links, JsonOptions);
        return Ok(body);
    }

    [HttpPost]
    public async Task<IActionResult> Assign(Guid orgId, AssignUserRole request, CancellationToken cancellationToken)
    {
        var assignment = await userRoles.AssignAsync(request, cancellationToken).ConfigureAwait(false);

        var links = new Dictionary<string, HalLink> { ["self"] = Links.Build(HttpContext, nameof(List), "UserRoles", new { orgId }) };
        var body = HalEnvelope.Wrap(assignment, links, JsonOptions);
        return Created(Links.Build(HttpContext, nameof(List), "UserRoles", new { orgId }).Href, body);
    }

    [HttpDelete("{userId}/{roleId:guid}")]
    public async Task<IActionResult> Unassign(Guid orgId, string userId, Guid roleId, CancellationToken cancellationToken)
    {
        var removed = await userRoles.UnassignAsync(userId, roleId, cancellationToken).ConfigureAwait(false);
        return removed ? NoContent() : NotFound();
    }
}
