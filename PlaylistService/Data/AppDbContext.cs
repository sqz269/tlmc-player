using Microsoft.EntityFrameworkCore;
using PlaylistService.Model;

namespace PlaylistService.Data;

public class AppDbContext : DbContext
{
    public DbSet<Playlist> Playlists { get; set; }
    public DbSet<PlaylistItem> PlaylistItems { get; set; }

    public AppDbContext(DbContextOptions<AppDbContext> opt) : base(opt)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Playlist>()
            .Property(p => p.Visibility)
            .HasConversion(
                v => v.ToString(),
                v => Enum.Parse<PlaylistVisibility>(v));

        modelBuilder.Entity<Playlist>()
            .Property(p => p.Type)
            .HasConversion(
                v => v.ToString(),
                v => Enum.Parse<PlaylistType>(v));

        modelBuilder.Entity<Playlist>()
            .Navigation(p => p.Tracks)
            .AutoInclude();

        base.OnModelCreating(modelBuilder);
    }
}