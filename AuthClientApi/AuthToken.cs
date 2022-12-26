using System.Text.Json.Serialization;

namespace AuthServiceClientApi;

public class AuthToken
{
    [JsonPropertyName("iss")]
    public string Issuer { get; set; }

    [JsonPropertyName("iat")]
    public long IssuedAt { get; set; }

    [JsonPropertyName("exp")]
    public long Expiration { get; set; }

    [JsonPropertyName("user")]
    public string User { get; set; }

    [JsonPropertyName("user_id")]
    public string UserId { get; set; }

    [JsonPropertyName("roles")]
    public List<string> Roles { get; set; }
}
