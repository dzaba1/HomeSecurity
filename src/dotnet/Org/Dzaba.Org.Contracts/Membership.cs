using System.ComponentModel.DataAnnotations;

namespace Dzaba.Org.Contracts;

public class Membership
{
    public Guid TenantId { get; set; }

    [Required(AllowEmptyStrings = false)]
    public string UserId { get; set; }

    // Roles maybe?
}
