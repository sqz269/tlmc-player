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

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Album>().Navigation(a => a.Tracks).AutoInclude();
        modelBuilder.Entity<Track>().Navigation(t => t.Original).AutoInclude();
        modelBuilder.Entity<OriginalTrack>().Navigation(og => og.Album).AutoInclude();

        base.OnModelCreating(modelBuilder);
    }
}