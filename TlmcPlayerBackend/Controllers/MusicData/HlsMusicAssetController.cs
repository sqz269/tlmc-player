using Microsoft.AspNetCore.Mvc;
using MimeDetective.Storage;
using TlmcPlayerBackend.Data.Api.MusicData;
using TlmcPlayerBackend.Models.MusicData;

namespace TlmcPlayerBackend.Controllers.MusicData;

[ApiController]
[Route("api/asset/track/{trackId:Guid}/hls")]
public class HlsMusicAssetController : Controller
{
    private readonly IHlsPlaylistRepo _hlsPlaylistRepo;
    private readonly LinkGenerator _linkGenerator;
    private readonly ILogger<HlsMusicAssetController> _logger;

    public HlsMusicAssetController(IHlsPlaylistRepo hlsPlaylistRepo, LinkGenerator linkGenerator, ILogger<HlsMusicAssetController> logger)
    {
        this._hlsPlaylistRepo = hlsPlaylistRepo;
        _linkGenerator = linkGenerator;
        _logger = logger;   
    }

    [HttpGet("")]
    [HttpHead("")]
    public async Task<IActionResult> GetMasterPlaylist(Guid trackId)
    {
        var playlist = await _hlsPlaylistRepo.GetPlaylistForTrack(trackId, null);
        if (playlist == null)
            return NotFound();

        return Content(GenerateMasterPlaylist(await _hlsPlaylistRepo.GetPlaylistsForTrack(trackId), trackId), "application/vnd.apple.mpegurl");
    }

    private string GenerateMasterPlaylist(List<HlsPlaylist> playlists, Guid trackId)
    {
        var lines = new List<string>()
        {
            "#EXTM3U",
            "#EXT-X-VERSION:7",
            "#EXT-X-MEDIA:TYPE=AUDIO,GROUP-ID=\"audio\",NAME=\"Audio\",DEFAULT=YES,AUTOSELECT=YES"
        };

        foreach (var hlsPlaylist in playlists)
        {
            if (hlsPlaylist.Bitrate == null)
                continue;

            lines.Add(@$"#EXT-X-STREAM-INF:BANDWIDTH={hlsPlaylist.Bitrate}000,AUDIO=""audio"",CODECS=""mp4a.40.2""");
            lines.Add(_linkGenerator.GetUriByName(HttpContext, nameof(GetMediaPlaylist), new {trackId, quality=hlsPlaylist.Bitrate}, fragment: FragmentString.Empty));
        }

        return string.Join("\n", lines);
    }

    private async Task<string?> GetMediaPlaylistDefinition(Guid trackId, int quality)
    {
        var segments = await _hlsPlaylistRepo.GetSegmentsForTrack(trackId, quality);
        if (segments == null || segments.Count == 0)
            return null;

        var lines = new List<string>()
        {
            "#EXTM3U",
            "#EXT-X-VERSION:7",
            "#EXT-X-TARGETDURATION:10",
            "#EXT-X-MEDIA-SEQUENCE:0",
            "#EXT-X-PLAYLIST-TYPE:VOD"
        };

        // one of them is garenteed to be init.mp4
        var initMp4 = _linkGenerator.GetUriByName(HttpContext, nameof(GetSegment), new { trackId, quality, segment="init.mp4" });
        lines.Add($"#EXT-X-MAP:URI=\"{initMp4}\"");
        foreach (var segment in segments)
        {
            if (segment.Index < 0)
                continue; // skip init segment

            lines.Add($"#EXTINF:10.007800,"); // let's just put a fixed duration for now
            var segmentUrl = _linkGenerator.GetUriByName(HttpContext, nameof(GetSegment), new { trackId, quality, segment = segment.Name });
            lines.Add(segmentUrl);
        }
        lines.Add("#EXT-X-ENDLIST");

        return string.Join("\n", lines);
    }

    [HttpGet("{quality:int}k/playlist.m3u8", Name = nameof(GetMediaPlaylist))]
    [HttpHead("{quality:int}k/playlist.m3u8")]
    public async Task<IActionResult> GetMediaPlaylist(Guid trackId, int quality, [FromQuery] bool generated=false)
    {
        if (generated)
        {
            var generatedPlaylist = await GetMediaPlaylistDefinition(trackId, quality);
            if (generatedPlaylist == null)
                return NotFound();

            return Content(generatedPlaylist, "application/vnd.apple.mpegurl");
        }

        var playlist = await _hlsPlaylistRepo.GetPlaylistForTrack(trackId, quality);
        if (playlist == null)
            return NotFound();

        if (!System.IO.File.Exists(playlist.HlsPlaylistPath))
        {
            _logger.LogError("Physical Playlist File Not Found: {PlaylistHlsPlaylistPath}", playlist.HlsPlaylistPath);
            return Problem(statusCode: StatusCodes.Status500InternalServerError,
                title: "Internal Server Error: Read Playlist Failed", detail: "Physical Playlist File Not Found");
        }

        var content = await System.IO.File.ReadAllTextAsync(playlist.HlsPlaylistPath);

        return Content(content, "application/vnd.apple.mpegurl");
    }

    [HttpGet("{quality:int}k/{segment}", Name=nameof(GetSegment))]
    [HttpHead("{quality:int}k/{segment}")]
    public async Task<IActionResult> GetSegment(Guid trackId, int quality, string segment)
    {
        var seg = await _hlsPlaylistRepo.GetSegment(trackId, quality, segment);
        if (seg == null)
            return NotFound();

        if (!System.IO.File.Exists(seg.Path))
            return Problem(statusCode: StatusCodes.Status500InternalServerError,
                title: "Internal Server Error: Read Segment Failed", detail: "Physical Segment File Not Found");

        return PhysicalFile(seg.Path, "video/mp4", enableRangeProcessing: true);
    }
}