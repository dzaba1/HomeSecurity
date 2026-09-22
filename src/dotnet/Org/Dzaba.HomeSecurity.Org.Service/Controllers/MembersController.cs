using Asp.Versioning;
using Dzaba.AspNetUtils;
using Dzaba.AspNetUtils.ActionFilters;
using Dzaba.HomeSecurity.Domain;
using Dzaba.HomeSecurity.Org.Contracts;
using Dzaba.HomeSecurity.Org.Service.Hal;
using Dzaba.HomeSecurity.Org.Service.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace Dzaba.HomeSecurity.Org.Service.Controllers;

/// <summary>
/// List is "membership only" (plain [Authorize], gated by
/// TenantResolutionMiddleware); Add/Remove require org.manage_members - see
/// the endpoint table in the plan.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Authorize]
[ValidateModel]
[HandleErrors]
[Route("api/v{version:apiVersion}/orgs/{orgId:guid}/members")]
public sealed class MembersController : OrgControllerBase
{
    private readonly IMembershipsService memberships;
    private readonly IPermissionEvaluator permissionEvaluator;

    public MembersController(IMembershipsService memberships, IPermissionEvaluator permissionEvaluator,
        IOrgLinkFactory links, IOptions<JsonOptions> jsonOptions)
        : base(links, jsonOptions)
    {
        ArgumentNullException.ThrowIfNull(memberships);
        ArgumentNullException.ThrowIfNull(permissionEvaluator);

        this.memberships = memberships;
        this.permissionEvaluator = permissionEvaluator;
    }

    [HttpGet]
    public async Task<IActionResult> List(Guid orgId, CancellationToken cancellationToken)
    {
        var items = new List<Membership>();
        await foreach (var membership in memberships.ListAsync(cancellationToken))
        {
            items.Add(membership);
        }

        var links = new Dictionary<string, HalLink>
        {
            ["self"] = Links.Build(HttpContext, nameof(List), "Members", new { orgId }),
            ["organization"] = Links.Build(HttpContext, nameof(OrgsController.Get), "Orgs", new { orgId }),
        };

        var userId = User.GetUserSubOrNameIdentifier();
        if (await permissionEvaluator.HasPermissionAsync(userId, orgId, PermissionKeys.OrgManageMembers, cancellationToken).ConfigureAwait(false))
        {
            links["add"] = Links.Build(HttpContext, nameof(Add), "Members", new { orgId }, "POST");
        }

        var body = HalEnvelope.WrapCollection("members", items, links, JsonOptions);
        return Ok(body);
    }

    [HttpPost]
    [Authorize(Policy = PermissionKeys.OrgManageMembers)]
    public async Task<IActionResult> Add(Guid orgId, CreateMembership request, CancellationToken cancellationToken)
    {
        var membership = await memberships.AddAsync(request, cancellationToken).ConfigureAwait(false);

        var links = new Dictionary<string, HalLink>
        {
            ["self"] = Links.Build(HttpContext, nameof(List), "Members", new { orgId }),
            ["organization"] = Links.Build(HttpContext, nameof(OrgsController.Get), "Orgs", new { orgId }),
        };

        var body = HalEnvelope.Wrap(membership, links, JsonOptions);
        return Created(Links.Build(HttpContext, nameof(List), "Members", new { orgId }).Href, body);
    }

    [HttpDelete("{userId}")]
    [Authorize(Policy = PermissionKeys.OrgManageMembers)]
    public async Task<IActionResult> Remove(Guid orgId, string userId, CancellationToken cancellationToken)
    {
        var removed = await memberships.RemoveAsync(userId, cancellationToken).ConfigureAwait(false);
        return removed ? NoContent() : NotFound();
    }
}
