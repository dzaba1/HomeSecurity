using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Finbuckle.MultiTenant.Abstractions;

namespace Dzaba.Org;

public class GuidTenantInfo : ITenantInfo
{
    [Key]
    public Guid GuidId { get; set; }

    [NotMapped]
    public string Id
    {
        get => GuidId.ToString();
        set => GuidId = Guid.Parse(value);
    }

    [Required(AllowEmptyStrings = false)]
    [MaxLength(64)]
    public string Identifier { get; set; }

    [Required(AllowEmptyStrings = false)]
    [MaxLength(64)]
    public string Name { get; set; }
}