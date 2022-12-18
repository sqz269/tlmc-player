using System.Formats.Asn1;
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
        await GenerateThumbnailDomColor(dbContext, environment.IsProduction());
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

        var thumbSizeFilenameMap = new Dictionary<int, string>
        {
            { 50, "tiny.jpeg" },
            { 125, "small.jpeg" },
            { 250, "medium.jpeg" },
            { 350, "large.jpeg" },
        };

        var query = dbContext.Albums.Where(a => a.Thumbnail == null && a.AlbumImage != null)
            .Include(a => a.AlbumImage);

        var count = query.Count();
        var i = 0;

        foreach (var album in await query.ToListAsync())
        {
            i++;
            var albumThumbRoot = Path.Join(thumbroot, album.Id.ToString());
            Directory.CreateDirectory(albumThumbRoot);

            var assetMap = new Dictionary<string, Asset>();

            Console.WriteLine($"[{i}/{count}] Generating Thumb for {album.Id}");

            foreach (var (size, name) in thumbSizeFilenameMap)
            {
                var thumbPath = Path.Join(albumThumbRoot, name);

                await ThumbnailUtils.GenerateThumbImage(
                    album.AlbumImage.AssetPath, 
                    thumbPath,
                    size);

                var asset = new Asset
                {
                    AssetId = Guid.NewGuid(),
                    AssetMime = "image/jpeg",
                    AssetName = name,
                    AssetPath = thumbPath,
                    Large = false
                };

                assetMap[name] = asset;
            }

            var thumb = MakeThumbnailFromAssetMap(assetMap, album.AlbumImage);

            album.Thumbnail = thumb;
            // Insert to db
            await dbContext.Assets.AddRangeAsync(assetMap.Values);
            await dbContext.Thumbnails.AddAsync(thumb);

            await dbContext.SaveChangesAsync();
        }
    }

    private static Thumbnail MakeThumbnailFromAssetMap(IReadOnlyDictionary<string, Asset> assetMap, Asset original)
    {
        return new Thumbnail
        {
            Id = Guid.NewGuid(),
            Large = assetMap["large.jpeg"],
            Medium = assetMap["medium.jpeg"],
            Original = original,
            Small = assetMap["small.jpeg"],
            Tiny = assetMap["tiny.jpeg"]
        };
    }

    private static async Task GenerateThumbnailDomColor(AppDbContext dbContext, bool isProduction)
    {
        if (!isProduction) return;

        var noColorQuery = dbContext.Thumbnails
            .Where(t => t != null && t.Colors.Count == 0)
            .Include(t => t.Original);

        var count = await noColorQuery.CountAsync();

        var noColors = await noColorQuery.ToListAsync();

        int i = 0;
        foreach (Thumbnail thumbnail in noColors)
        {
            i++;
            Console.WriteLine($"[{i}/{count}] Generating Dom Colors for {thumbnail.Id}");
            var domColors = await ThumbnailUtils.CalculateNthDominantColor(thumbnail.Original.AssetPath);
            var colors = domColors.Select(d => d.Item1.ToHex()).ToList();
            thumbnail.Colors = colors;

            if (i % 300 == 0)
            {
                await dbContext.SaveChangesAsync();
                Console.WriteLine("Saving 300 changes");
            }
        }

        await dbContext.SaveChangesAsync();
        Console.WriteLine("Saving Changes");
    }
}