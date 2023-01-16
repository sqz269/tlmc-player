using System.Text;

namespace AuthServiceClientApi.KeyProviders;

public class JwtKeyFromConfiguration : IJwtKeyProvider
{
    private readonly IConfiguration _config;

    private readonly string? _privateKey;
    private readonly string? _publicKey;

    public JwtKeyFromConfiguration(IConfiguration configuration)
    {
        _config = configuration;

        var jwtSection = configuration.GetSection("JwtKeys");

        var valueType = jwtSection["Type"];
        var publicKey = jwtSection["PublicKey"];
        var privateKey = jwtSection["PrivateKey"];

        if (valueType == "File")
        {
            if (File.Exists(publicKey))
                _publicKey = File.ReadAllText(publicKey);

            if (File.Exists(privateKey))
                _privateKey = File.ReadAllText(privateKey);
        }
        else if (valueType == "Key")
        {
            _publicKey = publicKey;
            _privateKey = privateKey;
        }
        else
        {
            throw new InvalidOperationException(
                $"Invalid Type for Jwt Key Type. Expected: \"File\" or \"Key\". Got: {valueType}");
        }
    }

    public Task<string?> GetJwtRsPublicKey()
    {
        return Task.FromResult(_publicKey);
    }

    public Task<string?> GetJwtRsPrivateKey()
    {
        return Task.FromResult(_privateKey);
    }
}