using Dzaba.AspNetUtils;
using Dzaba.AspNetUtils.ActionFilters;
using Dzaba.Org.Contracts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.ComponentModel.DataAnnotations;

namespace Dzaba.Org.Service.Controllers;

[Route("v1/organizations")]
[HandleErrors]
public class OrganizationsController : ControllerBase, IOrganizationsService
{
    private readonly IOrganizationServiceInternal impl;

    public OrganizationsController(IOrganizationServiceInternal impl)
    {
        ArgumentNullException.ThrowIfNull(impl);

        this.impl = impl;
    }

    [HttpPost]
    [Authorize]
    [ValidateModel]
    public async Task<Organization> CreateOrgAsync([FromBody, Required] CreateOrganization organization)
    {
        var userId = User.GetUserSubOrNameIdentifier();
        return await impl.CreateOrgAsync(organization.Name, userId).ConfigureAwait(false);
    }
}
