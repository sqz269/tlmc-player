namespace TlmcPlayerBackend.Search;

/// <summary>
/// Meilisearch connection settings. With no Url configured the engine is treated as
/// absent: search answers 503, index pushes become logged no-ops, and nothing else
/// in the API depends on it (SCHEMA-V6.md section 7 — an engine outage degrades one
/// feature, never the site).
/// </summary>
public class SearchOptions
{
    public const string Section = "Meilisearch";

    public string? Url { get; set; }

    /// <summary>
    /// Sent as a bearer token. The deployment hands the master key to this single
    /// backend; switch to a derived search/admin key pair if anything else ever
    /// gets network reach to the engine.
    /// </summary>
    public string? ApiKey { get; set; }

    public string IndexUid { get; set; } = "tracks";

    public bool Enabled => !string.IsNullOrWhiteSpace(Url);
}
