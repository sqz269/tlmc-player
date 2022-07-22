﻿using System.ComponentModel.DataAnnotations;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using AuthServiceClientApi.KeyProviders;
using AuthServiceClientApi.Utils;
using Microsoft.IdentityModel.Tokens;

namespace AuthServiceClientApi;

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
    public string? PublicKey { get; private set; }

    private byte[]? _publicKey;
    private byte[]? _privateKey;
    private readonly IJwtKeyProvider _keyProvider;

    public JwtManager(IJwtKeyProvider keyProvider)
    {
        _keyProvider = keyProvider;

        UseKey(_keyProvider.GetJwtRsPrivateKey(), _keyProvider.GetJwtRsPublicKey());
    }

    private static string? FormatKey(string? key)
    {
        return key?
            .Trim()
            .Replace("-----BEGIN PRIVATE KEY-----", "")
            .Replace("-----END PRIVATE KEY-----", "")

            .Replace("-----BEGIN PUBLIC KEY-----", "")
            .Replace("-----END PUBLIC KEY-----", "")
            .Replace("\n", "");
    }

    public void UseKey(string? privateKey, string? publicKey)
    {
        PublicKey = FormatKey(publicKey);

        _privateKey = FormatKey(privateKey)?.B64DecodeBytes();
        _publicKey = FormatKey(publicKey)?.B64DecodeBytes();
    }

    private bool ValidateData(string payload, string signature)
    {
        using var rsa = new RSACryptoServiceProvider();
        rsa.ImportSubjectPublicKeyInfo(_publicKey, out _);
        return rsa.VerifyData(Encoding.UTF8.GetBytes(payload), signature.B64UrlDecodeBytes(),
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1);
    }

    public string GenerateJwt<T>(T payload)
    {
        if (_privateKey == null)
        {
            throw new InvalidOperationException("Cannot Generate JWT token without a private key configured");
        }

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

    /// <summary>
    /// 
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="serializedToken"></param>
    /// <returns></returns>
    /// <exception cref="InvalidOperationException"></exception>
    /// <exception cref="InvalidDataException"></exception>
    public T? DecodeJwt<T>(string serializedToken)
    {
        if (_publicKey == null)
        {
            throw new InvalidOperationException("Cannot Decode and Verify Jwt token without a public key configured");
        }

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

        var isPayloadValid = ValidateData(parts[0] + "." + parts[1], signature);
        if (!isPayloadValid)
        {
            throw new InvalidDataException("Invalid signature for JWT");
        }

        return payloadStruct;
    }
}