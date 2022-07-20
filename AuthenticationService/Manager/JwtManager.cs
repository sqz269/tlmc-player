using System.ComponentModel.DataAnnotations;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using AuthenticationService.Utils;
using Microsoft.IdentityModel.Tokens;

namespace AuthenticationService.Manager;

internal class JwtHeader
{
    [Required]
    [JsonPropertyName("alg")]
    public string Algorithm { get; set; }

    [Required]
    [JsonPropertyName("typ")]
    public string Type { get; set; } = "JWT";
}


public class JwtManager
{
    public string PublicKey { get; private set; }

    private byte[]? _publicKey;
    private byte[]? _privateKey;

    public JwtManager(IConfiguration configuration)
    {
        var jwtSection = configuration.GetSection("JwtKeys");

        var valueType = jwtSection["Type"];
        var publicKey = jwtSection["PublicKey"];
        var privateKey = jwtSection["PrivateKey"];

        switch (valueType)
        {
            case "File" when !File.Exists(publicKey):
                throw new FileNotFoundException($"Invalid Path for Public Key: {publicKey}");
            case "File" when !File.Exists(privateKey):
                throw new FileNotFoundException($"Invalid Path for Private Key: {privateKey}");
            case "File":
                UseKey(
                    File.ReadAllText(privateKey, Encoding.UTF8),
                    File.ReadAllText(publicKey, Encoding.UTF8)
                );
                break;


            case "Key" when string.IsNullOrWhiteSpace(publicKey):
                throw new InvalidOperationException("Invalid Key for Jwt Public Key");
            case "Key" when string.IsNullOrWhiteSpace(privateKey):
                throw new InvalidOperationException("Invalid Key for Jwt Private Key");
            case "Key":
                UseKey(privateKey, publicKey);
                break;


            default:
                throw new InvalidOperationException(
                    $"Invalid Type for Jwt Key Type. Expected: \"File\" or \"Key\". Got: {valueType}");
        }
    }

    public static string FormatKey(string key)
    {
        return key
            .Replace("-----BEGIN PRIVATE KEY-----", "")
            .Replace("-----END PRIVATE KEY-----", "")

            .Replace("-----BEGIN PUBLIC KEY-----", "")
            .Replace("-----END PUBLIC KEY-----", "")
            .Replace("\n", "");
    }

    public void UseKey(string privateKey, string publicKey)
    {
        PublicKey = FormatKey(publicKey);

        _privateKey = Convert.FromBase64String(FormatKey(privateKey));
        _publicKey = Convert.FromBase64String(FormatKey(publicKey));
    }

    public bool ValidateData(string payload, string signature)
    {
        using var rsa = new RSACryptoServiceProvider();
        rsa.ImportSubjectPublicKeyInfo(_publicKey, out _);
        return rsa.VerifyData(Encoding.UTF8.GetBytes(payload), signature.B64UrlDecodeBytes(),
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1);
    }

    public string GenerateJwt<T>(T payload)
    {
        var header = new JwtHeader
        {
            Algorithm = "RS256",
            Type = "JWT"
        };

        var serializedHeader =JsonSerializer.Serialize(header);
        var serializedPayload = JsonSerializer.Serialize(payload);

        var data = serializedHeader.Utf8ToBase64Url() + "." + serializedPayload.Utf8ToBase64Url();
        var stream = new MemoryStream(Encoding.UTF8.GetBytes(data));

        using var rsa = new RSACryptoServiceProvider();
        rsa.ImportPkcs8PrivateKey(_privateKey, out _);
        var sig = rsa.SignData(stream, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        var serializedSig = Base64UrlEncoder.Encode(sig);

        return data + "." + serializedSig;
    }

    public T? DecodeJwt<T>(string serializedToken)
    {
        var parts = serializedToken.Split(".");

        var header = parts[0].B64UrlDecodeString();
        var payload = parts[1].B64UrlDecodeString();
        var signature = parts[2];

        var headerStruct = JsonSerializer.Deserialize<JwtHeader>(header);
        var payloadStruct = JsonSerializer.Deserialize<T>(payload);

        if (headerStruct?.Algorithm != "RS256")
        {
            throw new InvalidOperationException($"Unable to Verify signature of JWT, JWT Algorithm is not RS256 (Got: {headerStruct?.Algorithm}");
        }

        var isPayloadValid = ValidateData(header + "." + payload, signature);
        if (!isPayloadValid)
        {
            throw new InvalidDataException("Invalid signature for JWT");
        }

        return payloadStruct;
    }
}