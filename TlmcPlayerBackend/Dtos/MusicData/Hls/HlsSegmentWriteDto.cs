namespace TlmcPlayerBackend.Dtos.MusicData.Hls;

public class HlsSegmentWriteDto
{
    public Guid Id { get; set; }

    public int Index { get; set; }

    public string Name { get; set; }

    public string HlsSegmentPath { get; set; }

    public Guid HlsPlaylistId { get; set; }
}