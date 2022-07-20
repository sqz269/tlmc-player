using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;
using AuthenticationService.Models.Db;

namespace AuthenticationService.Dtos;

public class AuthToken
{
    [JsonPropertyName("iss")]
    public string Issuer { get; set; }

    [JsonPropertyName("iat")]
    public long IssuedAt { get; set; }

    [JsonPropertyName("exp")]
    public long Expiration { get; set; }

    [JsonPropertyName("user_id")]
    public string UserId { get; set; }

    [JsonPropertyName("roles")]
    public List<string> Roles { get; set; }

    public static AuthToken FromUser(User user, TimeSpan expirationOffset)
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
