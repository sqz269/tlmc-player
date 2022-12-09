using System.Runtime.CompilerServices;
using FFMpegCore;
using Microsoft.EntityFrameworkCore;
using MusicDataService.Data.Api;
using MusicDataService.Data.Impl;
using MusicDataService.Models;
using MusicDataService.Utils;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace MusicDataService.Data;

public static class UpdateDb
{
    public static async Task Update(IApplicationBuilder application, IWebHostEnvironment environment)
    {
        using var serviceScope = application.ApplicationServices.CreateScope();
        var dbContext = serviceScope.ServiceProvider.GetService<AppDbContext>();
        await UpdateTrackDuration(dbContext, environment.IsProduction());
        await GenerateAlbumThumbnail(serviceScope, environment.IsProduction());
    }

    private static async Task UpdateTrackDuration(AppDbContext appDb, bool isProduction)
    {
        if (!isProduction)
            return;

        var totalTrackNoDuration = await appDb.Tracks.Where(t => t.Duration == null).CountAsync();
        Console.WriteLine($"Found {totalTrackNoDuration} tracks without duration. Adding Duration Information");

        var tracks = await appDb.Tracks.Where(t => t.Duration == null).Include(t => t.TrackFile).ToListAsync();
        var i = 0;
        int saved;
        foreach (var track in tracks)
        {
            i++;
            Console.WriteLine($"Probing Track: {track.Id} ({i}/{totalTrackNoDuration})");
            var trackInfo = await FFProbe.AnalyseAsync(track.TrackFile.AssetPath);
            track.Duration = trackInfo.Duration;

            if (i % 300 == 0)
            {
                saved = await appDb.SaveChangesAsync();
                Console.WriteLine($"Saved: {saved} Changes");
            }
        }

        saved = await appDb.SaveChangesAsync();
        Console.WriteLine($"Saved: {saved} Changes");
    }

    private static async Task GenerateAlbumThumbnail(IServiceScope serviceScope, bool isProduction)
    {
        if (!isProduction) return;
        
        var configuration = serviceScope.ServiceProvider.GetService<IConfiguration>();
        if (configuration == null)
        {
            throw new NullReferenceException("Failed to Get \"IConfiguration\" service");
        }

        var dbContext = serviceScope.ServiceProvider.GetService<AppDbContext>();
        var assetRepo = serviceScope.ServiceProvider.GetService<IAssetRepo>();

        var thumbroot = configuration["ThumbnailRoot"];

        if (!Directory.Exists(thumbroot))
        {
            throw new DirectoryNotFoundException($"Invalid Thumbnail root directory: {thumbroot}");
        }

        var thumbSizeFilenameMap = new Dictionary<Tuple<int, int>, string>
        {
            { new Tuple<int, int>(50, 50), "50x50_tiny.png" },
            { new Tuple<int, int>(125, 125), "125x125_small.png" },
            { new Tuple<int, int>(250, 250), "250x250_medium.png" },
            { new Tuple<int, int>(350, 350), "350x350_large.png" },
        };

        foreach (var album in 
                 await dbContext.Albums.Where(a => a.Thumbnail == null && a.AlbumImage != null)
                     .Include(a => a.AlbumImage)
                     .ToListAsync())
        {
            var albumThumbRoot = Path.Join(thumbroot, album.Id.ToString());
            Directory.CreateDirectory(albumThumbRoot);

            var assetMap = new Dictionary<string, Asset>();

            foreach (var (size, name) in thumbSizeFilenameMap)
            {
                var thumbPath = Path.Join(albumThumbRoot, name);

                await ThumbnailUtils.GenerateThumbImage(
                    album.AlbumImage.AssetPath, 
                    thumbPath,
                    size.Item1, size.Item2);

                var asset = new Asset
                {
                    AssetId = Guid.NewGuid(),
                    AssetMime = "image/png",
                    AssetName = name,
                    AssetPath = thumbPath,
                    Large = false
                };

                assetMap[name] = asset;

                var thumb = MakeThumbnailFromAssetMap(assetMap, album.AlbumImage);

                // Insert to db
                await dbContext.Assets.AddRangeAsync(assetMap.Values);
                await dbContext.Thumbnails.AddAsync(thumb);

                await dbContext.SaveChangesAsync();
            }
        }
    }

    private static Thumbnail MakeThumbnailFromAssetMap(IReadOnlyDictionary<string, Asset> assetMap, Asset original)
    {
        return new Thumbnail
        {
            Id = Guid.NewGuid(),
            Large = assetMap["350x350_large.png"],
            Medium = assetMap["250x250_medium.png"],
            Original = original,
            Small = assetMap["125x125_small.png"],
            Tiny = assetMap["50x50_tiny.png"]
        };
    }
}