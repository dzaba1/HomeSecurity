using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Dzaba.HomeSecurity.Auth.Model;

[Table("AuthUsers")]
public class User
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }

    [Required(AllowEmptyStrings = false)]
    [MaxLength(64)]
    public string Name { get; set; }

    [Required(AllowEmptyStrings = false)]
    [MaxLength(1024)]
    public string PasswordHash { get; set; }
}
