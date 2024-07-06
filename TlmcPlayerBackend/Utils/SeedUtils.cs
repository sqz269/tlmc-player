using System.Globalization;
using System.Text.RegularExpressions;

namespace TlmcPlayerBackend.Utils;

public static class SeedUtils
{
    static Regex _allNumbers = new(@"^(\d+)$");

    /// <summary>
    /// Convert a string into a seed between -1 and 1
    /// </summary>
    /// <param name="seed">The seed value</param>
    /// <returns></returns>
    public static double GetSeed(string? seed)
    {
        if (seed == null)
        {
            return new Random().NextDouble() * 2 - 1;
        }

        // Parse string to double
        if (double.TryParse(seed, out double seedValue))
        {
            return seedValue;
        }
        return 0;
    }

    public static string GetSeedString(string? original, double seedValue)
    {
        if (original == null)
        {
            return seedValue.ToString(CultureInfo.InvariantCulture);
        }
        
        return original;
    }

    public static double GenerateSeed()
    {
        return new Random().NextDouble() * 2 - 1;
    }
}