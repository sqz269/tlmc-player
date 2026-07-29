namespace TlmcPlayerBackend.Utils;

/// <summary>
/// Decides whether an asset path is one this deployment is willing to open.
///
/// Asset rows carry absolute filesystem paths written by the ETL, and
/// AssetController opens them directly, so the path is effectively part of the trust
/// boundary. Set <c>Assets:Roots</c> (an array of directories) to constrain reads to
/// the mounted library and thumbnail trees.
///
/// With no roots configured only the structural checks apply. That is deliberate:
/// existing deployments store paths this code has never seen, and silently refusing
/// to serve them all would be a worse failure than the one being prevented. Rooted
/// deployments get the containment check as well.
/// </summary>
public class AssetPathPolicy
{
    private readonly string[] _roots;

    public AssetPathPolicy(IConfiguration configuration)
    {
        _roots = (configuration.GetSection("Assets:Roots").Get<string[]>() ?? [])
            .Where(root => !string.IsNullOrWhiteSpace(root))
            .Select(root => Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar))
            .ToArray();
    }

    public bool IsConstrained => _roots.Length > 0;

    public bool IsAllowed(string? path)
    {
        if (string.IsNullOrWhiteSpace(path) || !Path.IsPathRooted(path))
        {
            return false;
        }

        string full;
        try
        {
            // Collapses "..", so a traversal segment cannot slip past the prefix
            // comparison below.
            full = Path.GetFullPath(path);
        }
        catch (Exception e) when (e is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return false;
        }

        if (_roots.Length == 0)
        {
            return true;
        }

        return _roots.Any(root =>
            full == root ||
            full.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.Ordinal));
    }
}
