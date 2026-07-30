using Newtonsoft.Json;

namespace TlmcPlayerBackend.Search;

/// <summary>
/// One document per track, fully denormalized — the engine never needs a join
/// (SCHEMA-V6.md section 7). Field names are explicit snake_case: they are a public
/// contract shared with highlights on the wire, so they must not depend on any
/// serializer-wide naming policy.
///
/// Localized text is split per locale into suffixed fields (`*_default`, `*_en`,
/// `*_zh`, `*_jp`) rather than nested objects, because the per-field locale
/// declarations in <see cref="SearchIndexContract"/> match on attribute patterns —
/// the suffix is what makes `["*_jp"] → jpn` expressible.
/// </summary>
public class TrackSearchDocument
{
    /// <summary>TypeID string (`trk_…`). Crockford base32 plus the prefix stays
    /// inside Meilisearch's `[A-Za-z0-9_-]` primary-key alphabet.</summary>
    [JsonProperty("id")]
    public string Id { get; set; } = null!;

    [JsonProperty("title_default")]
    public string TitleDefault { get; set; } = null!;

    [JsonProperty("title_en")]
    public string? TitleEn { get; set; }

    [JsonProperty("title_zh")]
    public string? TitleZh { get; set; }

    [JsonProperty("title_jp")]
    public string? TitleJp { get; set; }

    [JsonProperty("release_id")]
    public string ReleaseId { get; set; } = null!;

    [JsonProperty("release_title_default")]
    public string ReleaseTitleDefault { get; set; } = null!;

    [JsonProperty("release_title_en")]
    public string? ReleaseTitleEn { get; set; }

    [JsonProperty("release_title_zh")]
    public string? ReleaseTitleZh { get; set; }

    [JsonProperty("release_title_jp")]
    public string? ReleaseTitleJp { get; set; }

    /// <summary>ISO yyyy-MM-dd, for display in results.</summary>
    [JsonProperty("release_date")]
    public string? ReleaseDate { get; set; }

    [JsonProperty("catalog_number")]
    public string? CatalogNumber { get; set; }

    [JsonProperty("circle_names")]
    public List<string> CircleNames { get; set; } = [];

    /// <summary>Verbatim credit_name strings, every role. With the identity layer
    /// deferred there is nothing else they could be (SCHEMA-V6.md section 5).</summary>
    [JsonProperty("credits")]
    public List<string> Credits { get; set; } = [];

    [JsonProperty("original_song_titles_default")]
    public List<string> OriginalSongTitlesDefault { get; set; } = [];

    [JsonProperty("original_song_titles_en")]
    public List<string> OriginalSongTitlesEn { get; set; } = [];

    [JsonProperty("original_song_titles_zh")]
    public List<string> OriginalSongTitlesZh { get; set; } = [];

    [JsonProperty("original_song_titles_jp")]
    public List<string> OriginalSongTitlesJp { get; set; } = [];

    /// <summary>Full and short work names merged: "東方紅魔郷" and its full spelling
    /// are both things people type.</summary>
    [JsonProperty("original_work_titles_default")]
    public List<string> OriginalWorkTitlesDefault { get; set; } = [];

    [JsonProperty("original_work_titles_en")]
    public List<string> OriginalWorkTitlesEn { get; set; } = [];

    [JsonProperty("original_work_titles_zh")]
    public List<string> OriginalWorkTitlesZh { get; set; } = [];

    [JsonProperty("original_work_titles_jp")]
    public List<string> OriginalWorkTitlesJp { get; set; } = [];

    [JsonProperty("tags")]
    public List<string> Tags { get; set; } = [];

    [JsonProperty("duration_seconds")]
    public double? DurationSeconds { get; set; }

    [JsonProperty("has_media")]
    public bool HasMedia { get; set; }

    [JsonProperty("has_lyrics")]
    public bool HasLyrics { get; set; }
}

/// <summary>
/// The index definition: primary key, relevance order, and the per-attribute locale
/// declarations. One static definition so the settings a rebuild applies and the
/// settings the bootstrap applies can never drift.
/// </summary>
public static class SearchIndexContract
{
    public const string PrimaryKey = "id";

    /// <summary>Order is relevance order: a match in the track title outranks the
    /// same match in a release title, which outranks credits, and so on.</summary>
    public static readonly string[] SearchableAttributes =
    [
        "title_default", "title_jp", "title_en", "title_zh",
        "release_title_default", "release_title_jp", "release_title_en", "release_title_zh",
        "circle_names",
        "credits",
        "original_song_titles_default", "original_song_titles_jp",
        "original_song_titles_en", "original_song_titles_zh",
        "original_work_titles_default", "original_work_titles_jp",
        "original_work_titles_en", "original_work_titles_zh",
        "tags",
        "catalog_number",
    ];

    public static readonly string[] FilterableAttributes = ["has_media", "has_lyrics"];

    /// <summary>Locales a caller may pass on a query (Meilisearch `locales`).</summary>
    public static readonly string[] AllowedQueryLocales = ["jpn", "eng", "cmn"];

    /// <summary>
    /// The CJK trap fix (SCHEMA-V6.md section 7): whatlang classifies pure-kanji text
    /// as Mandarin, silently applying Chinese segmentation to Japanese titles.
    /// Declaring locales per attribute overrides detection. `*_default` is pinned to
    /// jpn because the default spelling in this catalogue is the original —
    /// overwhelmingly Japanese — title; the mixed romanized/Japanese fields
    /// (circles, credits, tags) are constrained to jpn+eng so Mandarin can never be
    /// guessed while detection still separates the two scripts that actually occur.
    /// </summary>
    public static object BuildSettings() => new
    {
        searchableAttributes = SearchableAttributes,
        filterableAttributes = FilterableAttributes,
        localizedAttributes = new object[]
        {
            new { attributePatterns = new[] { "*_jp", "*_default" }, locales = new[] { "jpn" } },
            new { attributePatterns = new[] { "*_zh" }, locales = new[] { "cmn" } },
            new { attributePatterns = new[] { "*_en" }, locales = new[] { "eng" } },
            new
            {
                attributePatterns = new[] { "circle_names", "credits", "tags", "catalog_number" },
                locales = new[] { "jpn", "eng" },
            },
        },
    };
}
