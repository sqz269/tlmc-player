namespace TlmcPlayerBackend.Dtos.MusicData.Lyrics;

public class RubyDto
{
    public int Index { get; set; }
    public int Length { get; set; }
    public string Text { get; set; }
}

public class LyricsTextDto
{
    public string Lang { get; set; }
    public string Text { get; set; }
    public List<RubyDto> Ruby { get; set; } = new();
}

public class LyricsLineDto
{
    public int Index { get; set; }
    public TimeSpan? Time { get; set; }
    public List<LyricsTextDto> Blocks { get; set; } = new();
}

public class LyricsVariantDto
{
    public string? Variant { get; set; }
    public List<LyricsLineDto> Lines { get; set; } = new();
}

public class LyricsReadDto
{
    public Guid Id { get; set; }
    public List<LyricsVariantDto> Variants { get; set; } = new();
    public string? ReferenceUrl { get; set; }
}