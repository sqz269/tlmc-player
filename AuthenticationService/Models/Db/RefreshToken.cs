using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace AuthenticationService.Models.Db;

public class KnownRoles
{
    public const string Default = "default";
    public const string Admin = "admin";
}

[Index(nameof(TokenId), IsUnique = true)]
public class RefreshToken
{
    [Key]
    public Guid TokenId { get; set; }
    public DateTime IssuedTime { get; set; }


    public Guid UserId { get; set; }
    public User User { get; set; }
}