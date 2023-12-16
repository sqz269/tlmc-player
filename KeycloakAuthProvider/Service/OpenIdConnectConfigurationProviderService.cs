using System.Net.Http.Json;
using System.Security.Cryptography.X509Certificates;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;

namespace KeycloakAuthProvider.Service;

public class OpenIdConnectConfigurationProviderService
{
    public string? RealmUrl { get; private set; }

    private readonly IConfigurationManager<OpenIdConnectConfiguration> _configurationManager;
    
    private readonly ILogger _logger;

    public OpenIdConnectConfigurationProviderService(IConfiguration configuration, ILogger<OpenIdConnectConfigurationProviderService> logger)
    {
        _logger = logger;

        var realmUrl = configuration.GetSection("Keycloak")["RealmUrl"];
        _logger.LogInformation($"Keycloak Realm URL: {realmUrl}");

        if (string.IsNullOrEmpty(realmUrl))
        {
            _logger.LogError("Keycloak Realm URL is not set");
            throw new Exception("Keycloak Realm URL is not set");
        }

        RealmUrl = realmUrl;

        var documentRetriever = new HttpDocumentRetriever { RequireHttps = true };
        _configurationManager = new ConfigurationManager<OpenIdConnectConfiguration>(
            $"{realmUrl}/.well-known/openid-configuration",
            new OpenIdConnectConfigurationRetriever(),
            documentRetriever
        );
    }

    private async Task<OpenIdConnectConfiguration> GetConfigurationAsync()
    {
        return await _configurationManager.GetConfigurationAsync(CancellationToken.None);
    }

    /// <summary>
    /// Retrieves the first key with field 'use' set as 'sig' (signing) from the JWKS endpoint
    /// </summary>
    /// <returns>A <see cref="SecurityKey"/> object representing the signing key</returns>
    /// <exception cref="Exception">No key with property "use": "sig"</exception>
    public async Task<SecurityKey> GetSigningKeyAsync()
    {
        var keys = (await GetConfigurationAsync()).SigningKeys;
        var key = keys.FirstOrDefault();

        _logger.LogInformation($"Found {keys.Count} keys from JWKS endpoint");

        if (key == null)
        {
            throw new Exception($"No signing key found");
        }
        
        return key;
    }

    public async Task<ICollection<SecurityKey>> GetSigningKeysAsync()
    {
        var keys = (await GetConfigurationAsync()).SigningKeys;
        _logger.LogInformation($"Found {keys.Count} keys from JWKS endpoint");
        return keys;
    }
}
