using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using MusicDataService.Controllers;
using MusicDataService.Data.Api;
using MusicDataService.Models;
using NpgsqlTypes;

namespace MusicDataService.Data.Impl;

public class AlbumRepo : IAlbumRepo
{
    private readonly AppDbContext _context;

    public AlbumRepo(AppDbContext context)
    {
        _context = context;
    }

    public async Task<bool> SaveChanges()
    {
        return await _context.SaveChangesAsync() >= 1;
    }

    public async Task<long> CountTotalAlbums()
    {
        return await _context.Albums.LongCountAsync();
    }

    public async Task<IEnumerable<Album>> GetAlbums(int start, int limit)
    {
        var albums = await _context.Albums.OrderBy(a => a.Id)
            .Skip(start).Take(limit)
            // TODO: Configure Auto-include
            .Include(a => a.Tracks)
            .Include(a => a.AlbumImage)
            .Include(a => a.Thumbnail)
            .Include(a => a.OtherFiles)
            .Include(a => a.AlbumArtist)
            .ToListAsync();

        return albums;
    }

    public async Task<IEnumerable<Album>> GetAlbumsFiltered(AlbumFilter filter, int start, int limit)
    {
        var query = _context.Albums.AsQueryable();
        if (filter.Title != null)
        {
            query = query.Where(a => Regex.IsMatch(a.AlbumName.Default, filter.Title, RegexOptions.IgnoreCase));
        }

        // TODO: Test to see if this really works
        if (filter.ReleaseDateBegin != null || filter.ReleaseDateEnd != null)
        {
            var lower = filter.ReleaseDateBegin ?? DateTime.MinValue;
            var upper = filter.ReleaseDateEnd ?? DateTime.MaxValue;

            NpgsqlRange<DateTime> range = new NpgsqlRange<DateTime>(lower, upper);
            query = query.Where(a => a.ReleaseDate != null && range.Contains(a.ReleaseDate.Value));
        }

        if (filter.Convention != null)
        {
            query = query.Where(a => Regex.IsMatch(a.ReleaseConvention, filter.Convention, RegexOptions.IgnoreCase));
        }

        if (filter.Catalog != null)
        {
            query = query.Where(a => Regex.IsMatch(a.CatalogNumber, filter.Catalog, RegexOptions.IgnoreCase));
        }

        // Need to first map the artist back to circles
        if (filter is { Artist: { }, ArtistId: null })
        {
            // It's probably better to query circles first then filter albums if filter.Artist exists
            var circle = await _context.Circles.Where(c => c.Name == filter.Artist).FirstOrDefaultAsync();
            if (circle == null)
                throw new KeyNotFoundException($"Invalid Artist '{filter.Artist}'");

            query = query.Where(a => a.AlbumArtist.Contains(circle));
        }

        if (filter.ArtistId != null)
        {
            var circle = await _context.Circles.Where(c => c.Id == filter.ArtistId).FirstOrDefaultAsync();
            if (circle == null)
                throw new KeyNotFoundException($"Invalid Artist Id: '{filter.ArtistId}");

            query = query.Where(a => a.AlbumArtist.Contains(circle));
        }

        query = query.OrderBy(a => a.Id)
            .Include(a => a.Tracks)
            .Include(a => a.AlbumImage)
            .Include(a => a.Thumbnail)
            .Include(a => a.OtherFiles)
            .Include(a => a.AlbumArtist)
            .Skip(start).Take(limit);


        return await query.ToListAsync();
    }

    public async Task<Album?> GetAlbum(Guid id)
    {
        var album = await _context.Albums.Where(a => a.Id == id)
            .Include(a => a.Tracks)!
            .ThenInclude(t => t.Original)
            .Include(a => a.Tracks)
            .ThenInclude(t => t.TrackFile)
            .Include(a => a.Thumbnail)
            .Include(a => a.OtherFiles)
            .Include(a => a.AlbumArtist)
            .Include(a => a.ChildAlbums)
            .Include(a => a.ParentAlbum)
            .Include(a => a.AlbumImage)
                .FirstOrDefaultAsync();

        return album;
    }

    public async Task<Tuple<IEnumerable<Album>, IEnumerable<Guid>>> GetAlbums(IList<Guid> albumIds)
    {
        var albums = await _context.Albums.Where(a => albumIds.Contains(a.Id))
            .OrderBy(a => a.Id)
            .Include(a => a.Tracks)!
            .ThenInclude(t => t.Original)
            .Include(a => a.Tracks)
            .ThenInclude(t => t.TrackFile)
            .Include(a => a.Thumbnail)
            .Include(a => a.OtherFiles)
            .Include(a => a.AlbumArtist)
            .ToListAsync();

        if (albums.Count == albumIds.Count)
        {
            return new Tuple<IEnumerable<Album>, IEnumerable<Guid>>(albums, Array.Empty<Guid>());
        }

        var notFound = albumIds.Except(albums.Select(a => a.Id));

        return new Tuple<IEnumerable<Album>, IEnumerable<Guid>>(albums, notFound);
    }

    public Task UpdateAlbumData(Guid id, Album album)
    {
        throw new NotImplementedException();
    }

    public async Task<Guid> AddAlbum(Album album)
    {
        if (album.Id == Guid.Empty)
        {
            album.Id = Guid.NewGuid();
        }

        var addedAlbum = await _context.Albums.AddAsync(album);

        return addedAlbum.Entity.Id;
    }

    public async Task<Track> AddTrackToAlbum(Guid albumId, Track track)
    {
        var album = await GetAlbum(albumId);
        if (album == null)
            throw new ArgumentNullException(nameof(albumId), "No such album with Id");

        if (track.Id == Guid.Empty)
        {
            track.Id = new Guid();
        }
        
        track.Album = album;

        var addedTrack = await _context.Tracks.AddAsync(track);
        album.Tracks.Add(addedTrack.Entity);

        return addedTrack.Entity;
    }

    public Task<Track> GetTrack(Guid id)
    {
        throw new NotImplementedException();
    }

    public async Task<IEnumerable<Track>> GetTracksFiltered(TrackFilter filter, int start, int limit)
    {
        throw new NotImplementedException();
    }

    public Task UpdateTrackData(Track track)
    {
        throw new NotImplementedException();
    }
}