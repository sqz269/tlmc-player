using TlmcPlayerBackend.Dtos.MusicData;
using TlmcPlayerBackend.Ids;
using TlmcPlayerBackend.Models.Playlist;

namespace TlmcPlayerBackend.Dtos.Playlist;

public class PlayEventWriteDto
{
    public TrackId TrackId { get; set; }
    public PlaySource Source { get; set; } = PlaySource.Unknown;
    public int? MsPlayed { get; set; }
}

public class PlayEventReadDto
{
    public DateTime PlayedAt { get; set; }
    public PlaySource Source { get; set; }
    public int? MsPlayed { get; set; }
    public TrackListItemDto Track { get; set; } = null!;
    public ReleaseSlimDto Release { get; set; } = null!;
    public ArtworkId? ArtworkId { get; set; }
}
