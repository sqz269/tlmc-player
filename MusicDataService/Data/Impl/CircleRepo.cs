﻿using Microsoft.EntityFrameworkCore;
using MusicDataService.Controllers;
using MusicDataService.Data.Api;
using MusicDataService.Dtos.Album;
using MusicDataService.Extensions;
using MusicDataService.Models;

namespace MusicDataService.Data.Impl;

public class CircleRepo : ICircleRepo
{
    private readonly AppDbContext _context;

    public CircleRepo(AppDbContext context)
    {
        _context = context;
    }
    
    public async Task<bool> SaveChanges()
    {
        return await _context.SaveChangesAsync() >= 1;
    }

    public async Task<IEnumerable<Circle>> GetCircles(int start, int limit)
    {
        return await _context.Circles.Skip(start).Take(limit).OrderBy(c => c.Name).ToListAsync();
    }

    public async Task<IEnumerable<Circle>> GetCircles(IEnumerable<Guid> ids)
    {
        return await _context.Circles.Where(c => ids.Contains(c.Id)).ToListAsync();
    }

    public async Task<IEnumerable<Album>?> GetCircleAlbums(Guid id, int start, int limit, AlbumOrderOptions sort, SortOrder sortOrder)
    {
        var albumQueryable = _context.Albums.Where(a => a.AlbumArtist.Any(c => c.Id == id) &&
                                                        ((a.NumberOfDiscs > 1 && a.DiscNumber == 0) ||
                                                         (a.NumberOfDiscs == 1 && a.DiscNumber == 1)));
        albumQueryable = sort switch
        {
            AlbumOrderOptions.Id => albumQueryable.OrderByEx(a => a.Id, sortOrder),
            AlbumOrderOptions.Date => albumQueryable.OrderByEx(a => a.ReleaseDate, sortOrder),
            AlbumOrderOptions.Title => albumQueryable.OrderByEx(a => a.AlbumName.Default, sortOrder),
            _ => throw new ArgumentOutOfRangeException(nameof(sort), sort, null)
        };

        albumQueryable = albumQueryable.Skip(start).Take(limit)
            .Include(a => a.Thumbnail)
            .Include(a => a.AlbumArtist);

        return await albumQueryable.ToListAsync();
    }

    public async Task<IEnumerable<Album>?> GetCircleAlbums(string name, int start, int limit, AlbumOrderOptions sort, SortOrder sortOrder)
    {
        var circle = await _context.Circles
            .Where(c => c.Name == name || c.Alias.Contains(name))
            .IgnoreAutoIncludes()
            .FirstOrDefaultAsync();

        if (circle == null)
        {
            return null;
        }

        //return await _context.Albums
        //    .Where(a => a.AlbumArtist.Any(c => c.Id == circle.Id) &&
        //                ((a.NumberOfDiscs > 1 && a.DiscNumber == 0) || (a.NumberOfDiscs == 1 && a.DiscNumber == 1)))
        //    .OrderBy(a => a.Id)
        //    .Skip(start).Take(limit)
        //    .ToListAsync();
        return await GetCircleAlbums(circle.Id, start, limit, sort, sortOrder);
    }

    public async Task<Circle?> GetCircleByName(string name)
    {
        return await _context.Circles.Where(c => 
                EF.Functions.ILike(c.Name, name))
            .FirstOrDefaultAsync();
    }

    public async Task<Circle?> GetCircleById(Guid id)
    {
        return await _context.Circles.Where(c => c.Id == id).FirstOrDefaultAsync();
    }

    public async Task<Guid> AddCircle(Circle circle)
    {
        if (circle.Id == Guid.Empty)
        {
            circle.Id = Guid.NewGuid();
        }

        var added = await _context.Circles.AddAsync(circle);
        return added.Entity.Id;
    }

    public async Task<string> AddCircleWebsite(Guid circleId, CircleWebsite website)
    {
        var circle = await _context.Circles.FindAsync(circleId);
        circle.Website.Add(website);
        return website.Url;
    }
}