using Finbuckle.MultiTenant.Abstractions;
using System.ComponentModel.DataAnnotations;

namespace Dzaba.HomeSecurity.Org;

internal class OrgTenantInfo : ITenantInfo
{
    [Required(AllowEmptyStrings = false)]
    public string Id { get; set; }

    [Required(AllowEmptyStrings = false)]
    public string Identifier { get; set; }

    [Required(AllowEmptyStrings = false)]
    [MaxLength(64)]
    public string Name { get; set; }
}