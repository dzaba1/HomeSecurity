using System.ComponentModel.DataAnnotations;

namespace Dzaba.HomeSecurity.Org.Contracts;

public class Organization
{
    public Guid Id { get; set; }

    [Required(AllowEmptyStrings = false)]
    [MaxLength(64)]
    public string Name { get; set; }
}
