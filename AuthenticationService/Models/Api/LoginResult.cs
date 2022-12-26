using AuthServiceClientApi;

namespace AuthenticationService.Models.Api;

public class LoginResult
{
    public string JwtToken { get; set; }
    public string? RefreshToken { get; set; }
    public AuthToken AuthInfo { get; set; }
}