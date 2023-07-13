using Microsoft.EntityFrameworkCore;
using MusicDataService.Data.Api;
using MusicDataService.Models;

namespace MusicDataService.Data.Impl;

public class HlsPlaylistRepo : IHlsPlaylistRepo
{
    private readonly AppDbContext _dbContext;

    public HlsPlaylistRepo(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }
    
    public async Task<List<HlsPlaylist>> GetPlaylistsForTrack(Guid trackId)
    {
        return await _dbContext.HlsPlaylist.Where(a => a.TrackId == trackId).OrderBy(a => a.Bitrate).ToListAsync();
    }

    public async Task<HlsPlaylist?> GetPlaylistForTrack(Guid trackId, int? quality)
    {
        return await _dbContext.HlsPlaylist.Where(a => a.TrackId == trackId && a.Bitrate == quality)
            .FirstOrDefaultAsync();
    }

    public async Task<HlsSegment?> GetSegment(Guid trackId, int quality, string segment)
    {
        return await _dbContext.HlsSegment.Where(a =>
                a.HlsPlaylist.TrackId == trackId && a.HlsPlaylist.Bitrate == quality && a.Name == segment)
            .FirstOrDefaultAsync();
    }
}