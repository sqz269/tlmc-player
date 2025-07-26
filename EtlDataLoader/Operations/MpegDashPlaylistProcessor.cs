using EtlDataLoader.ExternalModel;
using EtlDataLoader.Utils;
using NetTopologySuite.Index.Bintree;
using NetTopologySuite.Utilities;
using Newtonsoft.Json;
using Sharprompt;
using TlmcPlayerBackend.Data;
using TlmcPlayerBackend.Models.MusicData;

namespace EtlDataLoader.Operations;

public static class MpegDashPlaylistProcessor
{
    public static string GetTrackIdKeyFromDashManifestPath(string manifestPath)
    {
        var dir = Path.GetDirectoryName(manifestPath);
        if (dir == null)
        {
            throw new InvalidDataException($"INVALID MANIFEST PATH {manifestPath}");
        }

        return dir;
    }

    /// <summary>
    /// Convert an absolute path to a media mount path by stripping all prefixes, and then remapping the prefix to the specified key directory.
    /// Also, replaces the path's separator to forward slashes (`/`) to ensure compatibility with web paths.
    /// 
    /// For example if given `\tlmc_staging\torrent\TLMC v5\[Floor：6]\2010.03.14 [F6R-003] Toho Synthesized [例大祭7]\(09) [Floor6] Energy Daybreak (Mark6's Future Dream Mix)` as the srcPath
    /// and keyDir as `TLMC v5`, and replaceDir is `/external_data/torrent
    /// The function will return `/external_data/torrent/TLMC v5/[Floor：6]/2010.03.14 [F6R-003] Toho Synthesized [例大祭7]/(09) [Floor6] Energy Daybreak (Mark6's Future Dream Mix)`
    /// 
    /// </summary>
    /// <param name="path">The source path to strip from.</param>
    /// <param name="keyDir">The directory name to use as the strip anchor.</param>
    /// <param name="replaceDir">The directory name to insert before the keyDir</param>
    /// <returns>The relative path starting from the key directory, or the original path if the key directory is not found.</returns>
    public static string ConvertMediaMntPath(string path, string keyDir, string replaceDir)
    {
        if (string.IsNullOrEmpty(path) || string.IsNullOrEmpty(keyDir) || string.IsNullOrEmpty(replaceDir))
            return path;

        // Step 1: Strip to relative part starting at keyDir
        string? relativePath = StripTlmcPrefixRoot(path, keyDir);
        if (relativePath == null)
            return path;

        // Step 2: Combine with replaceDir using Path.Combine (OS-specific)
        string combinedPath = Path.Combine(replaceDir, relativePath);

        // Step 3: Normalize to use forward slashes for web compatibility
        string webCompatiblePath = combinedPath.Replace(Path.DirectorySeparatorChar, '/');

        return webCompatiblePath;
    }


    /// <summary>
    /// Strips all prefixes up until some key directory
    ///
    /// For example if given `\tlmc_staging\torrent\TLMC v5\[Floor：6]\2010.03.14 [F6R-003] Toho Synthesized [例大祭7]\(09) [Floor6] Energy Daybreak (Mark6's Future Dream Mix)` as the srcPath
    /// and keyDir as TLMC v5, function returns `TLMC v5\[Floor：6]\2010.03.14 [F6R-003] Toho Synthesized [例大祭7]\(09) [Floor6] Energy Daybreak (Mark6's Future Dream Mix)`
    /// 
    /// </summary>
    /// <param name="srcPath">The source path to strip from.</param>
    /// <param name="keyDir">The directory name to use as the strip anchor.</param>
    /// <returns>The relative path starting from the key directory, or the original path if the key directory is not found.</returns>
    public static string? StripTlmcPrefixRoot(string? srcPath, string keyDir)
    {
        if (string.IsNullOrEmpty(srcPath) || string.IsNullOrEmpty(keyDir))
            return srcPath;

        // Normalize to OS-specific separators and split
        var parts = srcPath.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

        // Find index of key directory
        int index = Array.FindIndex(parts, p => string.Equals(p, keyDir, StringComparison.OrdinalIgnoreCase));
        if (index == -1)
        {
            // keyDir not found, return original path
            return srcPath;
        }

        // Join back from the keyDir onwards
        var resultParts = parts[index..];
        return Path.Combine(resultParts);
    }

    public static void PushMpegDashPlaylists(AppDbContext dbContext)
    {
        var filelistFp = Prompt.Input<string>("Enter path to dash-repackage.filelist.output.json", validators: [Validators.Required(), PathValidator.ValidateFilePath()]);

        // For example, thw two directory could be: \tlmc_staging\torrent\TLMC v5\[Floor：6]\2010.03.14 [F6R-003] Toho Synthesized [例大祭7]\(09) [Floor6] Energy Daybreak (Mark6's Future Dream Mix)
        // but the HLS path is \external_data\TLMC v5\[Floor：6]\2010.03.14 [F6R-003] Toho Synthesized [例大祭7]\(09) [Floor6] Energy Daybreak (Mark6's Future Dream Mix)
        // Although the same file, we need to strip them down to both TLMC v5\[Floor：6]\2010.03.14 [F6R-003] Toho Synthesized [例大祭7]\(09) [Floor6] Energy Daybreak (Mark6's Future Dream Mix)
        var tlmcRootDirPrefix = Prompt.Input<string>("Enter root folder containing TLMC v5", defaultValue: "TLMC v5");

        var mediaMntRoot = Prompt.Input<string>("Enter the media mount root directory (e.g. /external_data/torrent)", defaultValue: "/external_data/torrent");

        Console.WriteLine("Loading MPD manifest list");
        var targets = JsonConvert.DeserializeObject<List<HlsDashCvtList>>(File.ReadAllText(filelistFp));
        if  (targets == null || targets.Count == 0)
        {
            Console.WriteLine("No HLS MPD Targets found in the file");
            return;
        }

        // get all master playlists from the HLS Playlists
        var masterPlaylists = dbContext.HlsPlaylist
            .Where(p => p.Type == HlsPlaylistType.Master)
            .ToList();
        Console.WriteLine($"Found {masterPlaylists.Count} Master Playlists in the database");

        Dictionary<string, Guid> masterPlaylistToTrackId = new Dictionary<string, Guid>();

        foreach (var masterPlaylist in masterPlaylists)
        {
            var trackRootDir = StripTlmcPrefixRoot(Path.GetDirectoryName(masterPlaylist.HlsPlaylistPath), tlmcRootDirPrefix);
            if (trackRootDir == null)
            {
                throw new InvalidDataException($"Invalid HlsPlaylist path: {masterPlaylist.HlsPlaylistPath}");
            }
            masterPlaylistToTrackId[trackRootDir] = masterPlaylist.TrackId;
        }

        Dictionary<Guid, string> trackIdToMpdManifest = new();
        int failedToMatch = 0;
        int matched = 0;
        foreach (var dashTarget in targets)
        {
            var exists = masterPlaylistToTrackId.TryGetValue(StripTlmcPrefixRoot(GetTrackIdKeyFromDashManifestPath(dashTarget.OutputMpd), tlmcRootDirPrefix) ?? throw new InvalidOperationException(), out var guid);
            if (!exists)
            {
                failedToMatch++;
                Console.WriteLine($"[{failedToMatch}] Failed to find a existing HLS/TrackID For manifest: {dashTarget.OutputMpd}");
                continue;
            }

            matched++;
            trackIdToMpdManifest.Add(guid, dashTarget.OutputMpd);
        }

        // Prepare objects to insert
        List<DashPlaylist> dashPlaylistsDb = new List<DashPlaylist>();
        foreach (var kvp in trackIdToMpdManifest)
        {
            var guid = Guid.NewGuid();
            var dashPlaylist = new DashPlaylist
            {
                Id = guid,
                DashPlaylistPath = ConvertMediaMntPath(kvp.Value, tlmcRootDirPrefix, mediaMntRoot),
                TrackId = kvp.Key
            };

            dashPlaylistsDb.Add(dashPlaylist);
        }
        

        // dry run
        var proceed = Prompt.Confirm($"About to insert [{dashPlaylistsDb.Count}] DASH Playlists into the database. Continue?", defaultValue: true);
        if (!proceed)
        {
            Console.WriteLine("Aborting");
            return;
        }
        Console.WriteLine("Inserting records");
        // Batch insert, every 1000 records
        for(int i = 0; i < dashPlaylistsDb.Count; i += 1000)
        {
            var batch = dashPlaylistsDb.Skip(i).Take(1000).ToList();
            dbContext.DashPlaylists.AddRange(batch);
            dbContext.SaveChanges();
            Console.WriteLine($"Inserted {batch.Count} DASH Playlists into the database [PROGRESS: {i}/{dashPlaylistsDb.Count}]");
        }

        Console.WriteLine($"Successfully inserted {dashPlaylistsDb.Count} DASH Playlists into the database");
    }
}