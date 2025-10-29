using TlmcPlayerBackend.Models.MusicData;

namespace TlmcPlayerBackend.Data.Api.MusicData;

public interface IDashPlaylistRepo
{
    public Task<DashPlaylist?> GetDashManifestForTrack(Guid trackId);

    public Task<HlsSegment?> GetDashSegment(Guid trackId, int quality, string segment);
}