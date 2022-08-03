using Microsoft.EntityFrameworkCore;
using MusicDataService.Models;

namespace MusicDataService.Data;

public class AppDbContext : DbContext
{
    public DbSet<Album> Albums { get; set; }
    public DbSet<Track> Tracks { get; set; }
    public DbSet<OriginalTrack> OriginalTracks { get; set; }
    public DbSet<OriginalAlbum> OriginalAlbums { get; set; }

    public AppDbContext(DbContextOptions<AppDbContext> opt) : base(opt)
    {
    }
}