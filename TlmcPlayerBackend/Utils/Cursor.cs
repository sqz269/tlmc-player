using System.Text;
using Microsoft.AspNetCore.WebUtilities;
using Newtonsoft.Json;

namespace TlmcPlayerBackend.Utils;

/// <summary>
/// Opaque keyset-pagination cursors: a JSON array of the last row's sort values,
/// base64url-encoded. List endpoints return one in the envelope instead of an
/// offset (SCHEMA-V6.md section 15).
/// </summary>
public static class Cursor
{
    public static string Encode(params string?[] parts)
        => WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(parts)));

    /// <summary>Returns null for a missing or malformed cursor of the wrong arity.</summary>
    public static string?[]? Decode(string? cursor, int expectedParts)
    {
        if (string.IsNullOrEmpty(cursor))
        {
            return null;
        }

        try
        {
            var parts = JsonConvert.DeserializeObject<string?[]>(
                Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(cursor)));
            return parts?.Length == expectedParts ? parts : null;
        }
        catch (Exception)
        {
            return null;
        }
    }
}
