using Dzaba.AspNetUtils.ActionFilters;
using Dzaba.Org.Contracts;
using Microsoft.AspNetCore.Mvc;

namespace Dzaba.Org.Service.Controllers;

[Route("v1/permissions")]
[HandleErrors]
public class PermissionsController : ControllerBase, IPermissionsService
{

}
