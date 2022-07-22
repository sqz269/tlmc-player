namespace AuthServiceClientApi.KeyProviders;

public interface IJwtKeyProvider
{
    public string? GetJwtRsPublicKey();
    public string? GetJwtRsPrivateKey();
}