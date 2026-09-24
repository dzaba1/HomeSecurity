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
/// No action here needs a permission policy: Create/ListMine are "auth
/// only" (no {orgId} exists yet to check membership/permission against),
/// and Get is "membership only", already gated by TenantResolutionMiddleware
/// - see the endpoint table in the plan. HasPermissionAsync in Get is only
/// used to decide whether the addMember HAL link is included, not to gate
/// the endpoint itself.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Authorize]
[ValidateModel]
[HandleErrors]
[Route("api/v{version:apiVersion}/orgs")]
public sealed class OrgsController : HalControllerBase
{
    private readonly IOrganizationsService organizations;
    private readonly IPermissionEvaluator permissionEvaluator;

    public OrgsController(IOrganizationsService organizations, IPermissionEvaluator permissionEvaluator,
        ILinkFactory links, IOptions<JsonOptions> jsonOptions)
        : base(links, jsonOptions)
    {
        ArgumentNullException.ThrowIfNull(organizations);
        ArgumentNullException.ThrowIfNull(permissionEvaluator);

        this.organizations = organizations;
        this.permissionEvaluator = permissionEvaluator;
    }

    [HttpPost]
    public async Task<IActionResult> Create(CreateOrganization request, CancellationToken cancellationToken)
    {
        var userId = User.GetUserSubOrNameIdentifier();
        var org = await organizations.CreateAsync(request, userId, cancellationToken).ConfigureAwait(false);

        var links = new Dictionary<string, HalLink>
        {
            ["self"] = Links.Build(HttpContext, nameof(Get), "Orgs", new { orgId = org.Id }),
            ["members"] = Links.Build(HttpContext, nameof(MembersController.List), "Members", new { orgId = org.Id }),
            ["roles"] = Links.Build(HttpContext, nameof(RolesController.List), "Roles", new { orgId = org.Id }),
        };

        var body = HalEnvelope.Wrap(org, links, JsonOptions);
        return CreatedAtAction(nameof(Get), new { orgId = org.Id }, body);
    }

    [HttpGet]
    public async Task<IActionResult> ListMine(CancellationToken cancellationToken)
    {
        var userId = User.GetUserSubOrNameIdentifier();

        var items = new List<Organization>();
        await foreach (var org in organizations.ListForUserAsync(userId, cancellationToken))
        {
            items.Add(org);
        }

        var links = new Dictionary<string, HalLink> { ["self"] = Links.Build(HttpContext, nameof(ListMine), "Orgs") };
        var body = HalEnvelope.WrapCollection("orgs", items, links, JsonOptions,
            org => new Dictionary<string, HalLink> { ["self"] = Links.Build(HttpContext, nameof(Get), "Orgs", new { orgId = org.Id }) });

        return Ok(body);
    }

    [HttpGet("{orgId:guid}")]
    public async Task<IActionResult> Get(Guid orgId, CancellationToken cancellationToken)
    {
        var org = await organizations.GetAsync(orgId, cancellationToken).ConfigureAwait(false);
        if (org is null)
        {
            return NotFound();
        }

        var userId = User.GetUserSubOrNameIdentifier();
        var links = new Dictionary<string, HalLink>
        {
            ["self"] = Links.Build(HttpContext, nameof(Get), "Orgs", new { orgId }),
            ["members"] = Links.Build(HttpContext, nameof(MembersController.List), "Members", new { orgId }),
            ["roles"] = Links.Build(HttpContext, nameof(RolesController.List), "Roles", new { orgId }),
            ["permissions"] = Links.Build(HttpContext, nameof(PermissionsController.List), "Permissions"),
        };

        // The link's mere presence is the capability check - see
        // docs/architecture/13-hateoas-public-api.md.
        if (await permissionEvaluator.HasPermissionAsync(userId, orgId, PermissionKeys.OrgManageMembers, cancellationToken).ConfigureAwait(false))
        {
            links["addMember"] = Links.Build(HttpContext, nameof(MembersController.Add), "Members", new { orgId }, "POST");
        }

        var body = HalEnvelope.Wrap(org, links, JsonOptions);
        return Ok(body);
    }
}
