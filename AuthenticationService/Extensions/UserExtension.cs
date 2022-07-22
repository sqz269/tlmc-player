using AuthenticationService.Models.Db;
using AuthServiceClientApi;

namespace AuthenticationService.Extensions;

public static class UserExtension
{
    public static AuthToken ToAuthToken(this User user, TimeSpan expirationOffset)
    {
        var now = DateTimeOffset.Now;
        var exp = now.Add(expirationOffset);

        return new AuthToken
        {
            Issuer = "",
            IssuedAt = now.ToUnixTimeMilliseconds(),
            Expiration = exp.ToUnixTimeMilliseconds(),
            UserId = user.UserId.ToString(),
            Roles = user.Roles.ToList().ConvertAll(c => c.RoleName)
        };
    }
}