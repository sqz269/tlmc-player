using TlmcPlayerBackend.Models.MusicData;

namespace TlmcPlayerBackend.Data.Api.MusicData;

public interface IHlsPlaylistRepo
{
    public Task<HlsPlaylist?> GetPlaylistForTrack(Guid trackId, int? quality);
    public Task<List<HlsPlaylist>> GetPlaylistsForTrack(Guid trackId);
    public Task<HlsSegment?> GetSegment(Guid trackId, int quality, string segment);
}