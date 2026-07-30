using System.Buffers.Binary;

namespace TlmcPlayerBackend.Ids;

/// <summary>
/// TypeID (https://github.com/jetify-com/typeid) formatting: a lowercase type prefix,
/// an underscore, then the 128-bit id as 26 characters of Crockford base32. The suffix
/// preserves byte order, so ids over UUIDv7 sort by creation time as strings.
/// Implemented in-repo because the reference implementations are Go/TypeScript;
/// exercised against the spec's test vectors (see Docs/SCHEMA-V6.md section 1).
/// </summary>
public static class TypeId
{
    // Crockford base32: no i, l, o, u. Lowercase per the TypeID spec.
    private const string Alphabet = "0123456789abcdefghjkmnpqrstvwxyz";

    private static readonly sbyte[] DecodeTable = BuildDecodeTable();

    public static string Format(string prefix, Guid value)
    {
        Span<byte> bytes = stackalloc byte[16];
        value.TryWriteBytes(bytes, bigEndian: true, out _);
        var v = new UInt128(
            BinaryPrimitives.ReadUInt64BigEndian(bytes),
            BinaryPrimitives.ReadUInt64BigEndian(bytes[8..]));

        // Per the spec, an empty prefix means no underscore either.
        Span<char> chars = stackalloc char[prefix.Length == 0 ? 26 : prefix.Length + 1 + 26];
        if (prefix.Length > 0)
        {
            prefix.CopyTo(chars);
            chars[prefix.Length] = '_';
        }

        var suffix = chars[^26..];
        // 26 x 5 = 130 bits; the top two shift out as zero, so the first character
        // carries only the uuid's top 3 bits and is always '0'..'7'.
        for (var i = 25; i >= 0; i--)
        {
            suffix[i] = Alphabet[(int)((uint)v & 31)];
            v >>= 5;
        }

        return new string(chars);
    }

    public static Guid Parse(string prefix, string input)
    {
        if (!TryParse(prefix, input, out var value))
        {
            throw new FormatException($"'{input}' is not a valid '{prefix}_' TypeID.");
        }

        return value;
    }

    public static bool TryParse(string prefix, ReadOnlySpan<char> input, out Guid value)
    {
        value = default;
        var expectedLength = prefix.Length == 0 ? 26 : prefix.Length + 1 + 26;
        if (input.Length != expectedLength)
        {
            return false;
        }

        if (prefix.Length > 0 && (!input.StartsWith(prefix) || input[prefix.Length] != '_'))
        {
            return false;
        }

        var suffix = input[^26..];
        UInt128 v = 0;
        foreach (var c in suffix)
        {
            if (c > 127)
            {
                return false;
            }

            var d = DecodeTable[c];
            if (d < 0)
            {
                return false;
            }

            v = (v << 5) | (uint)d;
        }

        // A first character above '7' would overflow 128 bits (see Format).
        if (DecodeTable[suffix[0]] > 7)
        {
            return false;
        }

        Span<byte> bytes = stackalloc byte[16];
        BinaryPrimitives.WriteUInt64BigEndian(bytes, (ulong)(v >> 64));
        BinaryPrimitives.WriteUInt64BigEndian(bytes[8..], (ulong)v);
        value = new Guid(bytes, bigEndian: true);
        return true;
    }

    private static sbyte[] BuildDecodeTable()
    {
        var table = new sbyte[128];
        Array.Fill(table, (sbyte)-1);
        for (var i = 0; i < Alphabet.Length; i++)
        {
            table[Alphabet[i]] = (sbyte)i;
        }

        return table;
    }
}
