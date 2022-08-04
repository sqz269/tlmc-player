using Microsoft.EntityFrameworkCore;
using MusicDataService.Models;

namespace MusicDataService.Data;

public class OriginalAlbumRepo : IOriginalAlbumRepo
{
    private readonly AppDbContext _context;

    public OriginalAlbumRepo(AppDbContext context)
    {
        _context = context;
    }

    public async Task<bool> SaveChanges()
    {
        return await _context.SaveChangesAsync() >= 1;
    }

    public async Task<OriginalAlbum?> GetOriginalAlbum(string id)
    {
        return await _context.OriginalAlbums.Where(a => a.Id == id).FirstOrDefaultAsync();
    }

    public async Task<string> AddOriginalAlbum(OriginalAlbum originalAlbum)
    {
        if (originalAlbum.Id == null)
        {
            throw new ArgumentNullException(nameof(originalAlbum.Id), "An Id for [OriginalAlbum] must be provided when adding an album");
        }

        var orgAlbum = await _context.OriginalAlbums.AddAsync(originalAlbum);
        return orgAlbum.Entity.Id;
    }
}