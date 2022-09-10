using FFMpegCore;
using Microsoft.EntityFrameworkCore;

namespace MusicDataService.Data;

public static class UpdateDb
{
    public static async Task Update(IApplicationBuilder application, IWebHostEnvironment environment)
    {
        using var serviceScope = application.ApplicationServices.CreateScope();
        var dbContext = serviceScope.ServiceProvider.GetService<AppDbContext>();
        await UpdateTrackDuration(dbContext, environment.IsProduction());
    }

    private static async Task UpdateTrackDuration(AppDbContext appDb, bool isProduction)
    {
        if (!isProduction)
            return;

        var totalTrackNoDuration = await appDb.Tracks.Where(t => t.Duration == null).CountAsync();
        Console.WriteLine($"Found {totalTrackNoDuration} tracks without duration. Adding Duration Information");

        var tracks = await appDb.Tracks.Where(t => t.Duration == null).Include(t => t.TrackFile).ToListAsync();
        var i = 0;
        foreach (var track in tracks)
        {
            i++;
            Console.WriteLine($"Probing Track: {track.Id} ({i}/{totalTrackNoDuration})");
            var trackInfo = await FFProbe.AnalyseAsync(track.TrackFile.AssetPath);
            track.Duration = trackInfo.Duration;
        }
    }
}