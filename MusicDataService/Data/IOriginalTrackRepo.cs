using MusicDataService.Models;

namespace MusicDataService.Data;

public interface IOriginalTrackRepo
{
    public Task<bool> SaveChanges();

    public Task<OriginalTrack?> GetOriginalTrack(string trackId);

    public Task<string> AddOriginalTrack(string albumId, OriginalTrack originalTrack);
}