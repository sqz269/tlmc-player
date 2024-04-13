using TlmcPlayerBackend.Dtos.MusicData.OriginalAlbum;
using TlmcPlayerBackend.Models.MusicData;

namespace TlmcPlayerBackend.Dtos.MusicData.OriginalTrack;

public class OriginalTrackReadDto
{
    public string Id { get; set; }

    public LocalizedField Title { get; set; }

    public OriginalAlbumReadDto Album { get; set; }
}