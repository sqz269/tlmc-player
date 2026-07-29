using Microsoft.AspNetCore.Authentication;
using Newtonsoft.Json.Linq;
using System.Security.Claims;

namespace KeycloakAuthProvider.Authentication;

public class KeycloakClaimTransformer : IClaimsTransformation
{
    public Task<ClaimsPrincipal> TransformAsync(ClaimsPrincipal principal)
    {
        // Runs on every authenticated request, and may run more than once for the
        // same principal, so it has to be both defensive and idempotent. A throw
        // here fails the whole request, which would turn a realm misconfiguration
        // into a tenant-wide 500.
        if (principal.Identity is not ClaimsIdentity claimsIdentity ||
            !claimsIdentity.IsAuthenticated)
        {
            return Task.FromResult(principal);
        }

        var realmAccess = claimsIdentity.FindFirst(claim => claim.Type == "realm_access");
        if (realmAccess == null)
        {
            return Task.FromResult(principal);
        }

        // Token-supplied JSON: a realm whose realm_access is not an object, or has no
        // roles array, must not be fatal.
        JArray? roles;
        try
        {
            roles = JObject.Parse(realmAccess.Value)["roles"] as JArray;
        }
        catch (Newtonsoft.Json.JsonException)
        {
            return Task.FromResult(principal);
        }

        if (roles == null)
        {
            return Task.FromResult(principal);
        }

        foreach (var role in roles)
        {
            var name = role.ToString();

            // Without this check a second invocation appends a duplicate of every
            // role already on the identity.
            if (!claimsIdentity.HasClaim(ClaimTypes.Role, name))
            {
                claimsIdentity.AddClaim(new Claim(ClaimTypes.Role, name));
            }
        }

        return Task.FromResult(principal);
    }
}
