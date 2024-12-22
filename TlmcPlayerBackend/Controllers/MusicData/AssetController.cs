using System.Threading.Tasks;
using FFMpegCore.Arguments;
using Microsoft.AspNetCore.Mvc;
using TlmcPlayerBackend.Data.Api.MusicData;

namespace TlmcPlayerBackend.Controllers.MusicData;

[ApiController]
[Route("api/asset")]
public class AssetController : Controller
{
    private static readonly HashSet<string> PREVIEWABLE_MIME_TYPES = [
        "image/jpeg",
        "image/png",
        "image/gif",
        "image/bmp",
        "image/webp",
        "image/x-icon",
        "text/plain",
        "text/html",
        "text/xml",
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

    public AssetController(IAssetRepo assetRepo)
    {
        _assetRepo = assetRepo;
    }

    [HttpGet("{id:Guid}", Name = nameof(GetAsset))]
    public async Task<IActionResult> GetAsset(Guid id, [FromQuery] bool download = false)
    {
        var asset = await _assetRepo.GetAssetById(id);
        if (asset == null)
            return NotFound();

        var fileStream = new FileStream(asset.Path, FileMode.Open, FileAccess.Read, FileShare.Read);

        var mime = asset.Mime ?? "application/octet-stream";

        if (download)
        {
            return File(fileStream, mime, asset.Name, enableRangeProcessing: asset.Size > 1000000);
        }

        return File(fileStream, mime);
    }
}