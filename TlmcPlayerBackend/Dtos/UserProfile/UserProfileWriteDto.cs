using System.ComponentModel.DataAnnotations;

namespace TlmcPlayerBackend.Dtos.UserProfile;

public class UserProfileWriteDto
{
    [Required]
    [StringLength(50, MinimumLength = 3, ErrorMessage = "DisplayName must be between 3 - 50 characters long")]
    [RegularExpression(@"^[\p{L}\p{M}\p{N}\p{P}\p{S} ]+$", ErrorMessage = "DisplayName must not contain CONTROL or SEPARATOR category of symbols")]
    public string DisplayName { get; set; }
}