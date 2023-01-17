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

    public async Task<string?> GetJwtRsPublicKey()
    {
        return await _authDataService.GetPublicKey();
    }

    public Task<string?> GetJwtRsPrivateKey()
    {
        return Task.FromResult<>(null);
    }
}