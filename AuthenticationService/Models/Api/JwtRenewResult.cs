using AuthServiceClientApi;

namespace AuthenticationService.Models.Api;

public class JwtRenewResult
{
    public string Token { get; set; }
    public AuthToken AuthInfo { get; set; }
}