using TlmcPlayerBackend.Models.MusicData;

namespace TlmcPlayerBackend.Dtos.MusicData.Hls;

public class HlsPlaylistWriteDto
{
    public Guid Id { get; set; }

    public HlsPlaylistType Type { get; set; }

    // bitrate may be null when Type = Master
    public int? Bitrate { get; set; }

    public string HlsPlaylistPath { get; set; }

    public Guid TrackId { get; set; }
}