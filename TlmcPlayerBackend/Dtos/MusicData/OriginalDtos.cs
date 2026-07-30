using TlmcPlayerBackend.Ids;
using TlmcPlayerBackend.Models.MusicData;

namespace TlmcPlayerBackend.Dtos.MusicData;

public class OriginalSongReadDto
{
    public OriginalSongId Id { get; set; }
    public string ExternalKey { get; set; } = null!;
    public LocalizedField Title { get; set; } = null!;
    public short? TrackIndex { get; set; }
    public string? ExternalRef { get; set; }
}

public class OriginalWorkReadDto
{
    public OriginalWorkId Id { get; set; }
    public string ExternalKey { get; set; } = null!;
    public string WorkType { get; set; } = null!;
    public LocalizedField FullName { get; set; } = null!;
    public LocalizedField ShortName { get; set; } = null!;
    public string? ExternalRef { get; set; }
    public List<OriginalSongReadDto> Songs { get; set; } = [];
}

public class OriginalWorkWriteDto
{
    public string ExternalKey { get; set; } = null!;
    public string WorkType { get; set; } = null!;
    public LocalizedField FullName { get; set; } = null!;
    public LocalizedField ShortName { get; set; } = null!;
    public string? ExternalRef { get; set; }
}

public class OriginalSongWriteDto
{
    public string ExternalKey { get; set; } = null!;
    public LocalizedField Title { get; set; } = null!;
    public short? TrackIndex { get; set; }
    public string? ExternalRef { get; set; }
}
