using System.ComponentModel.DataAnnotations.Schema;
using TlmcPlayerBackend.Ids;

namespace TlmcPlayerBackend.Models.MusicData;

public class Ruby
{
    public required int Index { get; set; }
    public required int Length { get; set; }
    public required string Text { get; set; }
}

public class LyricsText
{
    public required string Lang { get; set; }
    public required string Text { get; set; }
    public List<Ruby> Ruby { get; set; } = [];
}

public class LyricsLine
{
    // Index MUST be specified, if timespan is specified also
    // then SHOULD be Index(A) > Index(B) given TimeSpan(A) > TimeSpan(B)
    // however, this is not necessary
    // Index specifies the order of the lines if
    // time was not specified, otherwise, time should always
    // be prioritized
    public required int Index { get; set; }
    public TimeSpan? Time { get; set; }
    public required List<LyricsText> Blocks { get; set; }
}

public class LyricsVariant
{
    public string? Variant { get; set; }
    public required List<LyricsLine> Lines { get; set; } = [];
}

public class Lyrics
{
    public LyricsId Id { get; set; }

    // The stored document is relayed to clients verbatim (pass-through, see
    // SCHEMA-V6.md section 15), so its keys are the API contract: snake_case,
    // same as the wire.
    [Column(TypeName = "jsonb")]
    public required List<LyricsVariant> Variants { get; set; }

    public string? ReferenceUrl { get; set; }
}
