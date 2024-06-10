using FFMpegCore;
using Microsoft.EntityFrameworkCore;
using System.Collections.Concurrent;
using TlmcPlayerBackend.Data.Api.MusicData;
using TlmcPlayerBackend.Models.MusicData;
using TlmcPlayerBackend.Utils;

namespace TlmcPlayerBackend.Data;

public static class UpdateDb
{
    public static async Task Update(IApplicationBuilder application, IWebHostEnvironment environment)
    {
        using var serviceScope = application.ApplicationServices.CreateScope();
        var dbContext = serviceScope.ServiceProvider.GetRequiredService<AppDbContext>();
        await UpdateTrackDuration(application.ApplicationServices, environment.IsProduction());
        await GenerateAlbumThumbnail(serviceScope, environment.IsProduction());
        await GenerateThumbnailDomColor(dbContext, environment.IsProduction());
    }

    private static async Task _UpdateTrackDurationOne(IServiceProvider services, Track track)
    {
        using var scope = services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        // We need to get the track's master playlist to probe the duration
        var masterPlaylist = await dbContext.HlsPlaylist
            .Where(p => p.TrackId == track.Id && p.Type == HlsPlaylistType.Master)
            .FirstOrDefaultAsync();

        if (masterPlaylist == null)
        {
            Console.WriteLine($"Failed to find Master Playlist for Track: {track.Id}");
            return;
        }

        var fileExists = Path.Exists(masterPlaylist.HlsPlaylistPath);
        Console.WriteLine($"Probing Track: {track.Id}: {masterPlaylist.HlsPlaylistPath}");

        var trackInfo = await FFProbe.AnalyseAsync(masterPlaylist.HlsPlaylistPath);
        track.Duration = trackInfo.Duration;

        // Save the changes to the track duration
        await dbContext.SaveChangesAsync();
    }

    private static async Task UpdateTrackDuration(IServiceProvider services, bool isProduction)
    {
        if (!isProduction)
            return;

        using var scope = services.CreateScope();
        var appDb = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var totalTrackNoDuration = await appDb.Tracks.Where(t => t.Duration == null).CountAsync();
        Console.WriteLine($"Found {totalTrackNoDuration} tracks without duration. Adding Duration Information");

        var tracks = await appDb.Tracks.Where(t => t.Duration == null).Include(t => t.TrackFile).ToListAsync();

        var tasks = new List<Task>();

        foreach (var track in tracks)
        {
            tasks.Add(Task.Run(async () => await _UpdateTrackDurationOne(services, track)));
        }

        await Task.WhenAll(tasks);

        Console.WriteLine("All changes saved.");
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

        var query = dbContext.Albums.Where(a => a.Thumbnail == null && a.Image != null)
            .Include(a => a.Image);

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
                    album.Image.Path, 
                    thumbPath,
                    size);

                long fileSize = new FileInfo(thumbPath).Length;

                var asset = new Asset
                {
                    Id = Guid.NewGuid(),
                    Mime = "image/jpeg",
                    Name = name,
                    Path = thumbPath,
                    Size = fileSize
                };

                assetMap[name] = asset;
            }

            var thumb = MakeThumbnailFromAssetMap(assetMap, album.Image);

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
            var domColors = await ThumbnailUtils.CalculateNthDominantColor(thumbnail.Original.Path);
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