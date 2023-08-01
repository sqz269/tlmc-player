using Microsoft.AspNetCore.Mvc;
using MusicDataService.Data;
using MusicDataService.Data.Api;
using MusicDataService.Models;

namespace MusicDataService.Controllers;

[ApiController]
[Route("api/asset/track/{trackId:Guid}")]
public class HlsAssetController : Controller
{
    private readonly IHlsPlaylistRepo _hlsPlaylistRepo;
    private readonly LinkGenerator _linkGenerator;

    public HlsAssetController(IHlsPlaylistRepo hlsPlaylistRepo, LinkGenerator linkGenerator)
    {
        this._hlsPlaylistRepo = hlsPlaylistRepo;
        _linkGenerator = linkGenerator;
    }

    [HttpGet("")]
    //[ProducesResponseType(typeof(string), StatusCodes.Status200OK, contentType: "application/vnd.apple.mpegurl")]
    public async Task<IActionResult> GetMasterPlaylist(Guid trackId)
    {
        var playlist = await _hlsPlaylistRepo.GetPlaylistForTrack(trackId, null);
        if (playlist == null)
            return NotFound();

        //return Ok(playlist);
        //if (!System.IO.File.Exists(playlist.HlsPlaylistPath))
        //    return Problem(statusCode: StatusCodes.Status500InternalServerError,
        //        title: "Internal Server Error: Read Playlist Failed", detail: "Physical Playlist File Not Found");

        //Response.ContentType = "application/vnd.apple.mpegurl";

        return Content(GenerateMasterPlaylist(await _hlsPlaylistRepo.GetPlaylistsForTrack(trackId), trackId), "application/vnd.apple.mpegurl");
        //var content = await System.IO.File.ReadAllTextAsync(playlist.HlsPlaylistPath);

        //return Ok(content);
    }

    private string GenerateMasterPlaylist(List<HlsPlaylist> playlists, Guid trackId)
    {
        var lines = new List<string>()
        {
            "#EXTM3U",
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

    [HttpGet("hls/{quality:int}k/playlist.m3u8", Name = nameof(GetMediaPlaylist))]
    //[ProducesResponseType(typeof(string), StatusCodes.Status200OK, contentType: "application/vnd.apple.mpegurl")]
    public async Task<IActionResult> GetMediaPlaylist(Guid trackId, int quality)
    {
        var playlist = await _hlsPlaylistRepo.GetPlaylistForTrack(trackId, quality);
        if (playlist == null)
            return NotFound();

        //return Ok(playlist);
        if (!System.IO.File.Exists(playlist.HlsPlaylistPath))
            return Problem(statusCode: StatusCodes.Status500InternalServerError,
                title: "Internal Server Error: Read Playlist Failed", detail: "Physical Playlist File Not Found");

        //Response.ContentType = "application/vnd.apple.mpegurl";

        var content = await System.IO.File.ReadAllTextAsync(playlist.HlsPlaylistPath);

        return Content(content, "application/vnd.apple.mpegurl");
    }

    [HttpGet("hls/{quality:int}k/{segment}")]
    //[Produces("audio/mp4")]
    public async Task<IActionResult> GetSegment(Guid trackId, int quality, string segment)
    {
        var seg = await _hlsPlaylistRepo.GetSegment(trackId, quality, segment);
        if (seg == null)
            return NotFound();

        //return Ok(seg);
        if (!System.IO.File.Exists(seg.HlsSegmentPath))
            return Problem(statusCode: StatusCodes.Status500InternalServerError,
                title: "Internal Server Error: Read Segment Failed", detail: "Physical Segment File Not Found");

        return PhysicalFile(seg.HlsSegmentPath, "audio/mp4", enableRangeProcessing: true);
    }
}