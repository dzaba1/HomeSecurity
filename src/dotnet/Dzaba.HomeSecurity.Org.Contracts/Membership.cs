using System.ComponentModel.DataAnnotations;

namespace Dzaba.HomeSecurity.Org.Contracts;

public class Membership
{
    [Required(AllowEmptyStrings = false)]
    public string TenantId { get; set; }

    [Required(AllowEmptyStrings = false)]
    public string UserId { get; set; }

    // Roles maybe?
}
