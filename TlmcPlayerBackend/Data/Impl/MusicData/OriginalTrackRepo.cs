using Microsoft.EntityFrameworkCore;
using TlmcPlayerBackend.Data.Api.MusicData;
using TlmcPlayerBackend.Models.MusicData;

namespace TlmcPlayerBackend.Data.Impl.MusicData;

public class OriginalTrackRepo : IOriginalTrackRepo
{
    private readonly AppDbContext _context;

    public OriginalTrackRepo(AppDbContext context)
    {
        _context = context;
    }

    public async Task<bool> SaveChanges()
    {
        return await _context.SaveChangesAsync() >= 1;
    }

    public async Task<IEnumerable<OriginalTrack>> GetOriginalTracks(int start, int limit)
    {
        return await _context.OriginalTracks.OrderBy(t => t.Id).Skip(start).Take(limit).ToListAsync();
    }

    public async Task<OriginalTrack?> GetOriginalTrack(string trackId)
    {
        return await _context.OriginalTracks.Where(t => t.Id == trackId).FirstOrDefaultAsync();
    }

    public async Task<IEnumerable<OriginalTrack>> GetOriginalTracks(IEnumerable<string> trackIds)
    {
        return await _context.OriginalTracks.Where(t => trackIds.Contains(t.Id)).ToListAsync();
    }
}