﻿using Microsoft.EntityFrameworkCore;
using MusicDataService.Models;

namespace MusicDataService.Data;

public class AppDbContext : DbContext
{
    public DbSet<Album> Albums { get; set; }
    public DbSet<Track> Tracks { get; set; }
    public DbSet<Circle> Circles { get; set; }
    public DbSet<OriginalTrack> OriginalTracks { get; set; }
    public DbSet<OriginalAlbum> OriginalAlbums { get; set; }
    public DbSet<Asset> Assets { get; set; }
    public DbSet<Thumbnail> Thumbnails { get; set; }

    public AppDbContext(DbContextOptions<AppDbContext> opt) : base(opt)
    {
    }
    
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Album>().Navigation(a => a.Thumbnail).AutoInclude();

        modelBuilder.Entity<Thumbnail>().Navigation(t => t.Tiny).AutoInclude();
        modelBuilder.Entity<Thumbnail>().Navigation(t => t.Small).AutoInclude();
        modelBuilder.Entity<Thumbnail>().Navigation(t => t.Medium).AutoInclude();
        modelBuilder.Entity<Thumbnail>().Navigation(t => t.Large).AutoInclude();
        modelBuilder.Entity<Thumbnail>().Navigation(t => t.Original).AutoInclude();

        modelBuilder.Entity<Album>().Navigation(a => a.Tracks).AutoInclude();
        modelBuilder.Entity<Album>().Navigation(a => a.OtherFiles).AutoInclude();

        //modelBuilder.Entity<Track>().Navigation(t => t.Original).AutoInclude();
        //modelBuilder.Entity<Track>().Navigation(t => t.TrackFile).AutoInclude();
        //modelBuilder.Entity<Track>().Navigation(t => t.Album).AutoInclude(false);

        //modelBuilder.Entity<OriginalTrack>().Navigation(og => og.Album).AutoInclude();

        //modelBuilder.Entity<OriginalTrack>()
        //    .HasMany(t => t.Tracks)
        //    .WithMany(r => r.Original)
        //    .UsingEntity(opt => opt.ToTable("OgTrackTrackRelation"));

        base.OnModelCreating(modelBuilder);
    }
}