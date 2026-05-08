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
    public async Task<Organization> CreateOrgAsync([FromBody, Required] CreateOrganization organization)
    {
        return await impl.CreateOrgAsync(organization.Name, "").ConfigureAwait(false);
    }
}
