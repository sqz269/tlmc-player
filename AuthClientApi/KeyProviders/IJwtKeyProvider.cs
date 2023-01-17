namespace AuthServiceClientApi.KeyProviders;

public interface IJwtKeyProvider
{
    public Task<string?> GetJwtRsPublicKey();
    public Task<string?> GetJwtRsPrivateKey();
}