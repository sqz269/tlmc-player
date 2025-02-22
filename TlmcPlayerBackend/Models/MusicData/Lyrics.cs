using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Newtonsoft.Json;

namespace TlmcPlayerBackend.Models.MusicData;

public class Ruby
{
    public required int Index;
    public required int Length;
    public required string Text;
}

public class LyricsText
{
    public required string Lang;
    public required string Text;
    public List<Ruby> Ruby = [];
}

public class LyricsLine
{
    public TimeSpan? Time;
    public required List<LyricsText> Blocks;
}

public class LyricsVariant
{
    public required string Variant { get; set; }
    public required List<LyricsLine> Lines { get; set; } = [];
}

public class Lyrics
{
    [Key]
    public required Guid Id { get; set; }

    [Column(TypeName = "jsonb")]
    public required List<LyricsVariant> Variants { get; set; }

    public string? ReferenceUrl { get; set; }
}