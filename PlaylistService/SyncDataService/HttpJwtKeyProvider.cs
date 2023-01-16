using AuthServiceClientApi.KeyProviders;

namespace PlaylistService.SyncDataService;

public class HttpJwtKeyProvider : IJwtKeyProvider
{
    private readonly IHttpClientFactory _httpClientFactory;
    private string? _jwtPublicKeyGetUrl;

    public HttpJwtKeyProvider(IConfiguration configuration, IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
        _jwtPublicKeyGetUrl = configuration["AuthDataService:JwtPkGetUrl"];
    }

    public string? GetJwtRsPublicKey()
    {
        _httpClientFactory.CreateClient()
    }

    public string? GetJwtRsPrivateKey()
    {
        return null;
    }
}