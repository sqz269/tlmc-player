namespace UserProfileService.Dtos;

public class UserProfileReadDto
{
    public Guid Id { get; set; } // Same Id corresponding to User's Keycloak ID
    public string DisplayName { get; set; }
    public DateTime DateJoined { get; set; }
}