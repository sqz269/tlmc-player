using TlmcPlayerBackend.Ids;
using TlmcPlayerBackend.Models.MusicData;

namespace TlmcPlayerBackend.Dtos.MusicData;

/// <summary>
/// The variants document is relayed as stored (pass-through: stored jsonb keys are
/// snake_case, same as the wire, so no re-mapping happens per request).
/// </summary>
public class LyricsReadDto
{
    public LyricsId Id { get; set; }
    public List<LyricsVariant> Variants { get; set; } = [];
    public string? ReferenceUrl { get; set; }
}

public class LyricsWriteDto
{
    public required List<LyricsVariant> Variants { get; set; }
    public string? ReferenceUrl { get; set; }
}
