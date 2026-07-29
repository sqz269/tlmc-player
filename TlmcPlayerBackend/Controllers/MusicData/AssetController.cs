using Microsoft.AspNetCore.Mvc;
using TlmcPlayerBackend.Data.Api.MusicData;
using TlmcPlayerBackend.Utils;

namespace TlmcPlayerBackend.Controllers.MusicData;

[ApiController]
[Route("api/asset")]
public class AssetController : Controller
{
    /// <summary>
    /// Types that may be rendered inline. text/html and text/xml were removed: an
    /// asset stored with either would execute as script in this API's own origin,
    /// which turns any bad row into stored XSS. Everything not listed is still
    /// served, just as an attachment.
    /// </summary>
    private static readonly HashSet<string> PREVIEWABLE_MIME_TYPES = [
        "image/jpeg",
        "image/png",
        "image/gif",
        "image/bmp",
        "image/webp",
        "image/x-icon",
        "text/plain",
        "video/mp4",
        "video/webm",
        "video/mpeg",
        "audio/mpeg3",
        "audio/ogg",
        "audio/mp4",
        "audio/flac",
        "audio/midi",
    ];

    private readonly IAssetRepo _assetRepo;
    private readonly AssetPathPolicy _pathPolicy;
    private readonly ILogger<AssetController> _logger;

    public AssetController(
        IAssetRepo assetRepo,
        AssetPathPolicy pathPolicy,
        ILogger<AssetController> logger)
    {
        _assetRepo = assetRepo;
        _pathPolicy = pathPolicy;
        _logger = logger;
    }

    [HttpGet("{id:Guid}", Name = nameof(GetAsset))]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetAsset(Guid id, [FromQuery] bool download = false)
    {
        var asset = await _assetRepo.GetAssetById(id);
        if (asset == null)
            return NotFound();

        if (!_pathPolicy.IsAllowed(asset.Path))
        {
            // Logged, never returned: the path is host filesystem layout.
            _logger.LogError("Asset {AssetId} points outside the permitted asset roots", id);
            return Problem(statusCode: StatusCodes.Status500InternalServerError,
                title: "Asset Unavailable", detail: "The stored asset path is not permitted.");
        }

        // The row can outlive the file -- a moved library or an unmounted volume, both
        // of which have happened here. Without this check the missing file threw
        // FileNotFoundException, answering 500 and, in development, disclosing the
        // absolute path in the response body.
        if (!System.IO.File.Exists(asset.Path))
        {
            _logger.LogError("Asset {AssetId} is registered but missing on disk", id);
            return Problem(statusCode: StatusCodes.Status500InternalServerError,
                title: "Asset Unavailable", detail: "The stored asset is missing on disk.");
        }

        var mime = asset.Mime ?? "application/octet-stream";

        // Belt and braces with the allow-list: stops a browser sniffing a payload into
        // a more dangerous type than the one declared.
        Response.Headers["X-Content-Type-Options"] = "nosniff";

        var fileStream = new FileStream(asset.Path, FileMode.Open, FileAccess.Read, FileShare.Read);

        // Anything outside the preview allow-list is forced to download rather than
        // being refused, so unusual-but-legitimate assets stay reachable.
        if (download || !PREVIEWABLE_MIME_TYPES.Contains(mime))
        {
            return File(fileStream, mime, asset.Name, enableRangeProcessing: asset.Size > 1000000);
        }

        return File(fileStream, mime, enableRangeProcessing: asset.Size > 1000000);
    }
}
