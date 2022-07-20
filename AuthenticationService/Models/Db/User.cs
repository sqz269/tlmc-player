using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace AuthenticationService.Models.Db;

[Index(nameof(UserId), nameof(UserName), IsUnique = true)]
public class User
{
    [Key]
    [Required]
    public Guid UserId { get; set; }

    [Required]
    public string UserName { get; set; }

    [Required]
    public string Password { get; set; }

    public ICollection<Role> Roles { get; set; } = new List<Role>();

    public ICollection<RefreshToken> Tokens { get; set; } = new List<RefreshToken>();
}