using AuthServiceClientApi.KeyProviders;
using MusicDataService.DataService;

namespace MusicDataService.Utils;

public class JwtKeyFromHttpAuthDataService : IJwtKeyProvider
{
    private readonly IAuthDataClient _authDataService;

    public JwtKeyFromHttpAuthDataService(IAuthDataClient authDataService)
    {
        _authDataService = authDataService;
    }

    public string? GetJwtRsPublicKey()
    {
        return _authDataService.GetPublicKey().Result;
    }

    public string? GetJwtRsPrivateKey()
    {
        return null;
    }
}