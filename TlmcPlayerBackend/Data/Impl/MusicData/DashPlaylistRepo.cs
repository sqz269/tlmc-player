using Microsoft.EntityFrameworkCore;
using TlmcPlayerBackend.Data.Api.MusicData;
using TlmcPlayerBackend.Models.MusicData;

namespace TlmcPlayerBackend.Data.Impl.MusicData;

public class DashPlaylistRepo : IDashPlaylistRepo
{
    private readonly AppDbContext _dbContext;

    public DashPlaylistRepo(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<DashPlaylist?> GetDashManifestForTrack(Guid trackId)
    {
        return await _dbContext.DashPlaylists
            .Where(d => d.TrackId == trackId)
            .FirstOrDefaultAsync();
    }

    public async Task<HlsSegment?> GetDashSegment(Guid trackId, int quality, string segment)
    {
        return await _dbContext.HlsSegment.Where(a =>
                a.HlsPlaylist.TrackId == trackId && a.HlsPlaylist.Bitrate == quality && a.Name == segment)
                .FirstOrDefaultAsync();
    }
}