using System.Security.Cryptography;
using System.Text;

namespace TlmcPlayerBackend.Subsonic;

/// <summary>
/// Key-string mechanics shared by the management endpoints and the /rest auth
/// filter. The stored sha256 is also the lookup key, so authenticating is one
/// indexed probe and a stolen database still contains no usable credential.
/// </summary>
public static class SubsonicApiKey
{
    private const string Prefix = "tlmc_";
    private const string Alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";
    private const int RandomChars = 32;

    public static string Generate()
        => Prefix + new string(RandomNumberGenerator.GetItems<char>(Alphabet, RandomChars));

    public static string Hash(string key)
        => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(key)));

    /// <summary>`tlmc_` plus the first eight random characters, for display lists.</summary>
    public static string DisplayPrefix(string key)
        => key.Length <= Prefix.Length + 8 ? key : key[..(Prefix.Length + 8)];
}
