using Microsoft.AspNetCore.Mvc;
using TlmcPlayerBackend.Data.Api.MusicData;

namespace TlmcPlayerBackend.Controllers.MusicData;


[ApiController]
[Route("api/asset/track/{trackId:Guid}/dash")]
public class MpegDashMusicAssetController : Controller
{
    private readonly IHlsPlaylistRepo _hlsPlaylistRepo;
    private readonly LinkGenerator _linkGenerator;
    private readonly ILogger<MpegDashMusicAssetController> _logger;

    public MpegDashMusicAssetController(IHlsPlaylistRepo hlsPlaylistRepo, LinkGenerator linkGenerator, ILogger<MpegDashMusicAssetController> logger)
    {
        _hlsPlaylistRepo = hlsPlaylistRepo;
        _linkGenerator = linkGenerator;
        _logger = logger;
    }

    // DASH MPD Endpoint
    [HttpGet("dash/manifest.mpd", Name = nameof(GetDashManifest))]
    public async Task<IActionResult> GetDashManifest(Guid trackId)
    {
        return Ok();
    }

    // DASH Segment Endpoint
    [HttpGet("dash/{quality:int}k/{segment}")]
    public async Task<IActionResult> GetDashSegment(Guid trackId, int quality, string segment)
    {
        return Ok();
    }
}