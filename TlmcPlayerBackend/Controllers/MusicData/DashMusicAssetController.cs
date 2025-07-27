using Microsoft.AspNetCore.Mvc;
using TlmcPlayerBackend.Data.Api.MusicData;

namespace TlmcPlayerBackend.Controllers.MusicData;


[ApiController]
[Route("api/asset/track/{trackId:Guid}/dash")]
public class DashMusicAssetController : Controller
{
    private readonly IDashPlaylistRepo _dashPlaylistRepo;
    private readonly LinkGenerator _linkGenerator;
    private readonly ILogger<DashMusicAssetController> _logger;

    public DashMusicAssetController(IDashPlaylistRepo dashPlaylistRepo, LinkGenerator linkGenerator, ILogger<DashMusicAssetController> logger)
    {
        _dashPlaylistRepo = dashPlaylistRepo;
        _linkGenerator = linkGenerator;
        _logger = logger;
    }

    // DASH MPD Endpoint
    [HttpGet("manifest.mpd", Name = nameof(GetDashManifest))]
    public async Task<IActionResult> GetDashManifest(Guid trackId)
    {
        var dashPlaylist = await _dashPlaylistRepo.GetDashManifestForTrack(trackId);

        if (dashPlaylist == null)
        {
            return NotFound();
        }

        return PhysicalFile(dashPlaylist.DashPlaylistPath, "application/dash+xml");
    }

    // DASH Segment Endpoint
    [HttpGet("{quality:int}k/{segment}")]
    public async Task<IActionResult> GetDashSegment(Guid trackId, int quality, string segment)
    {
        var seg = await _dashPlaylistRepo.GetDashSegment(trackId, quality, segment);

        if (seg == null)
        {
            return NotFound();
        }

        if (!System.IO.File.Exists(seg.Path))
        {
            _logger.LogError("Physical Segment File Not Found: {Path}", seg.Path);
            return Problem(statusCode: StatusCodes.Status500InternalServerError,
                title: "Internal Server Error: Read Segment Failed", detail: "Physical Segment File Not Found");
        }

        return PhysicalFile(seg.Path, "video/mp4", enableRangeProcessing: true);
    }
}