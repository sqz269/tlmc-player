using FFMpegCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Writers;
using TlmcPlayerBackend.Data.Api.MusicData;
using TlmcPlayerBackend.Models.MusicData;
using TlmcPlayerBackend.Utils;

namespace TlmcPlayerBackend.Data;

public static class UpdateDb
{
    public static async Task Update(IApplicationBuilder application, IWebHostEnvironment environment)
    {
        using var serviceScope = application.ApplicationServices.CreateScope();
        var dbContext = serviceScope.ServiceProvider.GetService<AppDbContext>();
        await UpdateTrackDuration(application, environment.IsProduction());
        await GenerateAlbumThumbnail(serviceScope, environment.IsProduction());
        await GenerateThumbnailDomColor(dbContext, environment.IsProduction());
    }

    private static async Task UpdateTrackDuration(IApplicationBuilder application, bool isProduction)
    {
        // if (!isProduction)
        //     return;

        using var serviceScope = application.ApplicationServices.CreateScope();
        var dbContext = serviceScope.ServiceProvider.GetService<AppDbContext>();

        var totalTrackNoDuration = await dbContext.Tracks.Where(t => t.Duration == null).CountAsync();
        Console.WriteLine($"Found {totalTrackNoDuration} tracks without duration. Adding Duration Information");

        const int batchSize = 1000; // Adjust batch size as needed
        var tracks = await dbContext.Tracks.Where(t => t.Duration == null).Include(t => t.TrackFile).ToListAsync();
        var totalBatches = (int)Math.Ceiling((double)tracks.Count / batchSize);

        var options = new ParallelOptions
        {
            MaxDegreeOfParallelism = Environment.ProcessorCount
        };

        for (int batchIndex = 0; batchIndex < totalBatches; batchIndex++)
        {
            var batch = tracks.Skip(batchIndex * batchSize).Take(batchSize).ToList();

            int batchTrackIndex = 0;
            await Parallel.ForEachAsync(batch, options, async (track, cancellationToken) =>
            {
                var scope = application.ApplicationServices.CreateScope();
                var appDb = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                try
                {
                    // We need to get the track's master playlist to probe the duration
                    var masterPlaylist = await appDb.HlsPlaylist
                        .Where(p => p.TrackId == track.Id && p.Type == HlsPlaylistType.Master)
                        .FirstOrDefaultAsync(cancellationToken);

                    if (masterPlaylist == null)
                    {
                        Console.WriteLine($"Failed to find Master Playlist for Track: {track.Id}");
                        return;
                    }

                    var currentIndex = Interlocked.Increment(ref batchTrackIndex);
                    Console.WriteLine($"Batch {batchIndex + 1}/{totalBatches}, Track {currentIndex}/{batch.Count}: Probing Track: {track.Id}: {masterPlaylist.HlsPlaylistPath}");

                    try
                    {
                        var trackInfo = await FFProbe.AnalyseAsync(masterPlaylist.HlsPlaylistPath);
                        track.Duration = trackInfo.Duration;

                        appDb.Tracks.Update(track);
                        await appDb.SaveChangesAsync(cancellationToken);
                    }
                    catch (FFMpegCore.Exceptions.FFMpegException)
                    {
                        return;
                    }
                }
                finally
                {
                    scope.Dispose();
                    await appDb.DisposeAsync();
                }
            });
        }

        Console.WriteLine("Finished updating track durations.");
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