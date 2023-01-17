using AuthServiceClientApi.KeyProviders;
using Newtonsoft.Json;
using System.Security.Cryptography.X509Certificates;

namespace PlaylistService.SyncDataService;

public class HttpJwtKeyProvider : IJwtKeyProvider
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly string? _jwtPublicKeyGetUrl;

    public HttpJwtKeyProvider(IConfiguration configuration, IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
        _jwtPublicKeyGetUrl = configuration["AuthDataService:JwtPkGetUrl"];
    }

    public async Task<string?> GetJwtRsPublicKey()
    {
        var httpClient = _httpClientFactory.CreateClient();
        var result = await httpClient.GetStringAsync(_jwtPublicKeyGetUrl);

        var definition = new { PublicKey = "" };
        var r = JsonConvert.DeserializeAnonymousType(result, definition);
        Console.WriteLine($"Got Public Key: {r?.PublicKey}");
        return r?.PublicKey;
    }

    public Task<string?> GetJwtRsPrivateKey()
    {
        return Task.FromResult<string?>(null);
    }
}