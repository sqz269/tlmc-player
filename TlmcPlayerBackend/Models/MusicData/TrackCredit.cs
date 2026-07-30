using TlmcPlayerBackend.Ids;

namespace TlmcPlayerBackend.Models.MusicData;

public enum CreditRole
{
    Arranger,
    // Distinct from arranger on purpose: for original doujin compositions the
    // 作曲 credit is the one that matters, and thwiki records it separately.
    Composer,
    Vocalist,
    Lyricist,
    Performer,
    Staff,
}

/// <summary>
/// What the source said — verbatim, ordered, immutable. The contributor identity
/// layer is deliberately absent (deferred; see SCHEMA-V6.md section 5): when it is
/// built it arrives as new tables plus a nullable FK here, with no rewrite of rows.
/// </summary>
public class TrackCredit
{
    public TrackId TrackId { get; set; }
    public Track Track { get; set; } = null!;

    public CreditRole Role { get; set; }

    /// <summary>Preserves the source's ordering within a role.</summary>
    public short Ordinal { get; set; }

    /// <summary>Never rewritten; this is what the API displays and search indexes.</summary>
    public string CreditName { get; set; } = null!;

    /// <summary>Generated column: lower(credit_name), for string-identity browse.</summary>
    public string? CreditNameSort { get; private set; }
}
