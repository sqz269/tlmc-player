using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace UserProfileService.Models;

public class UserProfile
{
    [Key]
    public Guid Id { get; set; } // Same Id corresponding to User's Keycloak ID

    [Required]
    [StringLength(50, MinimumLength = 3, ErrorMessage = "DisplayName must be between 3 - 50 characters long")]
    [RegularExpression(@"^[\p{L}\p{M}\p{N}\p{P}\p{S} ]+$", ErrorMessage = "DisplayName must not contain CONTROL or SEPARATOR category of symbols")]
    public string DisplayName { get; set; }

    [Required]
    public DateTime DateJoined { get; set; }
}