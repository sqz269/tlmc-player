using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TlmcPlayerBackend.Data;
using TlmcPlayerBackend.Ids;
using TlmcPlayerBackend.Models.MusicData;
using TlmcPlayerBackend.Utils;

namespace TlmcPlayerBackend.Controllers.MusicData;

/// <summary>
/// Serves assets and streaming media. Rows hold storage keys, never paths; every
/// path served here is composed from a configured root plus the key plus the layout
/// convention shared with hls_assignment.py:
///   &lt;media_key&gt;/playlist.m3u8            master
///   &lt;media_key&gt;/hls/&lt;rung&gt;/playlist.m3u8  media playlist
///   &lt;media_key&gt;/hls/&lt;rung&gt;/stream.m4s     media data (init segment as byte range)
///   &lt;media_key&gt;/manifest.mpd              DASH manifest
/// The DASH manifest's BaseURL entries have `hls/` stripped (dash-repackage.py), so
/// the segment route below re-inserts it when touching disk.
/// </summary>
[ApiController]
[Route("api/asset")]
public class MediaController(AppDbContext context, StorageRootResolver resolver) : ControllerBase
{
    private const string HlsPlaylistMime = "application/vnd.apple.mpegurl";
    private const string MediaSegmentMime = "video/iso.segment";
    private const string DashManifestMime = "application/dash+xml";

    private readonly AppDbContext _context = context;
    private readonly StorageRootResolver _resolver = resolver;

    [HttpGet("{id}")]
    public async Task<IActionResult> GetAsset(AssetId id)
    {
        var asset = await _context.Assets.AsNoTracking().FirstOrDefaultAsync(a => a.Id == id);
        if (asset == null)
        {
            return NotFound();
        }

        var path = _resolver.Resolve(asset.Root, asset.StorageKey);
        if (path == null || !System.IO.File.Exists(path))
        {
            return NotFound();
        }

        if (asset.ContentHash != null)
        {
            // Media is immutable; the sha256 is a strong validator for free.
            Response.Headers.ETag = $"\"{asset.ContentHash}\"";
        }

        return PhysicalFile(path, asset.Mime ?? "application/octet-stream", enableRangeProcessing: true);
    }

    [HttpGet("track/{trackId}/hls/playlist.m3u8")]
    public Task<IActionResult> GetHlsMaster(TrackId trackId)
        => ServeTrackMedia(trackId, HlsPlaylistMime, null, "playlist.m3u8");

    [HttpGet("track/{trackId}/hls/{bitrate:int}/playlist.m3u8")]
    public Task<IActionResult> GetHlsMediaPlaylist(TrackId trackId, int bitrate)
        => ServeTrackMedia(trackId, HlsPlaylistMime, bitrate, "hls", $"{bitrate}k", "playlist.m3u8");

    [HttpGet("track/{trackId}/hls/{bitrate:int}/stream.m4s")]
    public Task<IActionResult> GetHlsSegment(TrackId trackId, int bitrate)
        => ServeTrackMedia(trackId, MediaSegmentMime, bitrate, "hls", $"{bitrate}k", "stream.m4s");

    [HttpGet("track/{trackId}/dash/manifest.mpd")]
    public async Task<IActionResult> GetDashManifest(TrackId trackId)
    {
        var track = await GetMediaTrack(trackId);
        if (track?.MediaKey == null || !track.HasDash)
        {
            return NotFound();
        }

        return ServeFile(track.MediaKey, DashManifestMime, "manifest.mpd");
    }

    /// <summary>
    /// The manifest references `&lt;rung&gt;/stream.m4s` relative to itself (BaseURL with
    /// `hls/` stripped); on disk the file lives under `hls/&lt;rung&gt;/`.
    /// </summary>
    [HttpGet("track/{trackId}/dash/{bitrate:int}k/stream.m4s")]
    public Task<IActionResult> GetDashSegment(TrackId trackId, int bitrate)
        => ServeTrackMedia(trackId, MediaSegmentMime, bitrate, "hls", $"{bitrate}k", "stream.m4s");

    private async Task<IActionResult> ServeTrackMedia(
        TrackId trackId, string mime, int? bitrate, params string[] segments)
    {
        var track = await GetMediaTrack(trackId);
        if (track?.MediaKey == null)
        {
            return NotFound();
        }

        if (bitrate is { } rung && !track.HlsBitrates.Contains((short)rung))
        {
            return NotFound();
        }

        return ServeFile(track.MediaKey, mime, segments);
    }

    private IActionResult ServeFile(string mediaKey, string mime, params string[] segments)
    {
        var path = _resolver.Resolve(StorageRoot.Library, mediaKey, segments);
        if (path == null || !System.IO.File.Exists(path))
        {
            return NotFound();
        }

        return PhysicalFile(path, mime, enableRangeProcessing: true);
    }

    private Task<Track?> GetMediaTrack(TrackId trackId)
    {
        return _context.Tracks
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == trackId);
    }
}
