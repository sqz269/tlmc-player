using System.Linq.Expressions;
using System.Reflection.Metadata.Ecma335;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using MusicDataService.Controllers;
using MusicDataService.Data.Api;
using MusicDataService.Extensions;
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

    public async Task<Tuple<IEnumerable<Album>, long>> GetAlbums(int start, int limit, AlbumOrderOptions sort, SortOrder sortOrder)
    {
        var albumsQueryable = _context.Albums
            .Where(a => (a.NumberOfDiscs > 1 && a.DiscNumber == 0) || (a.NumberOfDiscs == 1 && a.DiscNumber == 1));

        var total = albumsQueryable.Count();

        albumsQueryable = sort switch
        {
            AlbumOrderOptions.Id => albumsQueryable.OrderByEx(a => a.Id, sortOrder),
            AlbumOrderOptions.Date => albumsQueryable.OrderByEx(a => a.ReleaseDate, sortOrder),
            AlbumOrderOptions.Title => albumsQueryable.OrderByEx(a => a.AlbumName.Default, sortOrder),
            _ => throw new ArgumentOutOfRangeException(nameof(sort), sort, null)
        };

 
        albumsQueryable = albumsQueryable.Skip(start).Take(limit)
            // TODO: Configure Auto-include
            .Include(a => a.Thumbnail)
            .Include(a => a.AlbumArtist);

        return new Tuple<IEnumerable<Album>, long>(await albumsQueryable.ToListAsync(), total);
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
        // If the query uses .Include(a => a.ChildAlbums) with self referencing type, it will cause SEVERE
        // performance penalty (without 10ms vs. with 5,000 ms exec time). Because EF Core will include all the
        // properties like Tracks, Thumbnails, etc. in the child types which would result in MASSIVE amount of joins
        // in the query.
        // What we need to do is to selectively include only the necessary properties for the self-referencing
        // navigation property ChildAlbums, excluding the related entities and their properties.
        // However, .Include doesn't allow us to selectively load/exclude navigation properties
        // To achieve this, we need use explicit loading like .Collection and .Reference
        var album = await _context.Albums.Where(a => a.Id == id)
            .Include(a => a.Tracks)!
            .ThenInclude(t => t.Original)
            .Include(a => a.Tracks)
            .ThenInclude(t => t.TrackFile)
            .Include(a => a.Thumbnail)
            .Include(a => a.OtherFiles)
            .Include(a => a.AlbumArtist)
            .Include(a => a.AlbumImage)
                .FirstOrDefaultAsync();

        switch (album)
        {
            case null:
                return null;

            case { NumberOfDiscs: > 1, DiscNumber: 0 }:
                await _context.Entry(album)
                    .Collection(a => a.ChildAlbums)
                    .LoadAsync();
                break;

            case { NumberOfDiscs: > 1, DiscNumber: not 0}:
                await _context.Entry(album)
                    .Reference(a => a.ParentAlbum)
                    .LoadAsync();
                break;
        }

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