using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Configuration;
using System.Security.Claims;

namespace KeycloakAuthProvider.Authentication;

public class ClaimTransformer : IClaimsTransformation
{
    private readonly string _realm;

    public ClaimTransformer(string realm)
    {
        _realm = realm;
    }

    public Task<ClaimsPrincipal> TransformAsync(ClaimsPrincipal principal)
    {
        ClaimsIdentity claimsIdentity = (ClaimsIdentity)principal.Identity;

        if (claimsIdentity.IsAuthenticated && claimsIdentity.HasClaim((claim) => claim.Type == "realm_access"))
        {
            var userRole = claimsIdentity.FindFirst((claim) => claim.Type == "realm_access");

            var content = Newtonsoft.Json.Linq.JObject.Parse(userRole.Value);

            foreach (var role in content["roles"])
            {
                claimsIdentity.AddClaim(new Claim(ClaimTypes.Role, role.ToString()));
            }
        }

        return Task.FromResult(principal);
    }
}