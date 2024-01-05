using Microsoft.EntityFrameworkCore;
using MusicDataService.Data.Api;
using MusicDataService.Models;

namespace MusicDataService.Data.Impl;

public class TrackRepo : ITrackRepo
{
    private readonly AppDbContext _context;

    public TrackRepo(AppDbContext context)
    {
        _context = context;
    }

    public async Task<bool> SaveChanges()
    {
        return await _context.SaveChangesAsync() >= 1;
    }

    public async Task<Track?> GetTrack(Guid trackId)
    {
        var track = await _context.Tracks.Where(t => t.Id == trackId)
            .Include(t => t.Original)
            .ThenInclude(og => og.Album)
            .Include(t => t.Album)
            .ThenInclude(a => a.Thumbnail)
            .Include(t => t.Album)
            .ThenInclude(a => a.AlbumArtist)
            .Include(t => t.TrackFile)
            .FirstOrDefaultAsync();

        //if (track != null)
        //{
        //    track.Album.Tracks = null;
        //}
        return track;
    }

    public async Task<Tuple<List<Track>, List<Guid>>> GetTracks(IList<Guid> tracks)
    {
        var entities = await _context.Tracks
            .Where(t => tracks.Contains(t.Id))
            .OrderBy(t => t.Id)
            .IgnoreAutoIncludes()
            .Include(t => t.Album.Thumbnail)
            .Include(t => t.Album.AlbumArtist)
            .Include(t => t.Album.Thumbnail.Tiny)
            .Include(t => t.Album.Thumbnail.Small)
            .Include(t => t.Album.Thumbnail.Medium)
            .Include(t => t.Album.Thumbnail.Large)
            .Include(t => t.Album.Thumbnail.Original)
            .Include(t => t.TrackFile)
            .ToListAsync();

        if (entities.Count == tracks.Count)
        {
            return new Tuple<List<Track>, List<Guid>>(entities, new List<Guid>());
        }

        var diff = tracks.Except(
                entities.Select(e => e.Id))
            .ToList();
        return new Tuple<List<Track>, List<Guid>>(entities, diff);
    }
    
    public async Task<Guid> CreateTrack(Guid albumId, Track track)
    {
        if (track.Id == Guid.Empty)
        {
            track.Id = Guid.NewGuid();
        }

        var album = await _context.Albums.Where(a => a.Id == albumId).FirstOrDefaultAsync();
        if (album == null)
        {
            throw new ArgumentException($"No album found with given Album Id: {albumId}", nameof(albumId));
        }

        track.Album = album;
        album.Tracks.Add(track);
        var addedTrack = await _context.Tracks.AddAsync(track);
        return addedTrack.Entity.Id;
    }

    public async Task<IEnumerable<Track>> SampleRandomTrack(int limit)
    {
        return await _context.Tracks.FromSqlRaw(@"
                                            SELECT *
                                            FROM ""Tracks""
                                            TABLESAMPLE BERNOULLI(0.1)
                                            ORDER BY random()
                                            LIMIT {0}", limit)
            .IgnoreAutoIncludes().ToListAsync();
    }

    public async Task<bool> UpdateTrack(Guid trackId, Track track)
    {
        throw new NotImplementedException();
    }
}