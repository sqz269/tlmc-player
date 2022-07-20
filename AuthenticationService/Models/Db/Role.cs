using System.ComponentModel.DataAnnotations;

namespace AuthenticationService.Models.Db;

public class Role
{
    [Key]
    public string RoleName { get; set; }

    public ICollection<User> Users { get; set; } = new List<User>();
}