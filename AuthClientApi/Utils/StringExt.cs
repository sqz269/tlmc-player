using Microsoft.IdentityModel.Tokens;

namespace AuthServiceClientApi.Utils;

public static class StringExt
{
    public static byte[] B64DecodeBytes(this string src)
    {
        return Convert.FromBase64String(src);
    }

    public static string B64UrlDecodeString(this string src)
    {
        return Base64UrlEncoder.Decode(src);
    }

    public static byte[] B64UrlDecodeBytes(this string src)
    {
        return Base64UrlEncoder.DecodeBytes(src);
    }

    public static string Utf8ToBase64Url(this string src)
    {
        return Base64UrlEncoder.Encode(src);
    }
}