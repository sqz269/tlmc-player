using TlmcPlayerBackend.Models.MusicData;

namespace TlmcPlayerBackend.Utils;

/// <summary>
/// Resolves (storage root, key) pairs to absolute filesystem paths. Roots are
/// deployment configuration (Storage:Roots:Library / Thumbnail / Generated); rows
/// only ever hold root-relative keys, so what is servable is decided here and the
/// database cannot name a file outside a configured root.
/// </summary>
public class StorageRootResolver
{
    private readonly Dictionary<StorageRoot, string> _roots = new();

    public StorageRootResolver(IConfiguration configuration)
    {
        foreach (var root in Enum.GetValues<StorageRoot>())
        {
            var configured = configuration[$"Storage:Roots:{root}"];
            if (!string.IsNullOrWhiteSpace(configured))
            {
                _roots[root] = Path.TrimEndingDirectorySeparator(Path.GetFullPath(configured));
            }
        }
    }

    public bool IsConfigured(StorageRoot root) => _roots.ContainsKey(root);

    /// <summary>
    /// Returns the absolute path for a key (plus optional further segments, e.g.
    /// media_key + "hls/128k/stream.m4s"), or null when the root is unconfigured,
    /// the key is malformed, or the result escapes the root.
    /// </summary>
    public string? Resolve(StorageRoot root, string key, params string[] segments)
    {
        if (!_roots.TryGetValue(root, out var basePath))
        {
            return null;
        }

        var relative = segments.Length == 0
            ? key
            : key + '/' + string.Join('/', segments);

        // Mirrors the asset_storage_key_shape CHECK: no empty keys, no absolute
        // keys, no '..' path segment. Backslashes and NUL are rejected outright.
        if (string.IsNullOrEmpty(relative) ||
            relative.StartsWith('/') ||
            relative.Contains('\\') ||
            relative.Contains('\0') ||
            relative.Split('/').Any(part => part is "" or "." or ".."))
        {
            return null;
        }

        var full = Path.GetFullPath(Path.Combine(basePath, relative));
        return full.StartsWith(basePath + Path.DirectorySeparatorChar, StringComparison.Ordinal)
            ? full
            : null;
    }
}
