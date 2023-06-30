using System.Security.Authentication;
using System.Security.Claims;

namespace KeycloakAuthProvider.Identity;

public static class ClaimsPrincipalExtension
{
    public static UserClaim ToUserClaim(this ClaimsPrincipal principal)
    {
        ClaimsIdentity claimsIdentity = principal.Identity as ClaimsIdentity;
        if (claimsIdentity == null) throw new ArgumentNullException(nameof(claimsIdentity));


        if (!Guid.TryParse(claimsIdentity.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var userId))
        {
            throw new AuthenticationException("Failed to get ClaimTypes.NameIdentifier (UserId) from Claims Identity");
        }

        var username = claimsIdentity.FindFirst("preferred_username")?.Value;
        if (username == null)
        {
            throw new AuthenticationException("Failed to get 'preferred_username' (Username) from Claims Identity");
        }
        return new UserClaim(userId, username);
    }
}