using AutoMapper;
using Microsoft.AspNetCore.Mvc;
using MusicDataService.Data.Api;
using MusicDataService.Dtos;
using MusicDataService.Extensions;
using MusicDataService.Models;

namespace MusicDataService.Controllers;

// Controllers for Internal use only. All actions should have [DevelopmentOnly] Attribute
[ApiController]
[Route("api/internal")]
public class InternalController : Controller
{
    private readonly IAlbumRepo _albumRepo;
    private readonly ITrackRepo _trackRepo;
    private readonly IOriginalTrackRepo _originalTrackRepo;
    private readonly IMapper _mapper;

    private readonly LinkGenerator _linkGenerator;
    private readonly Func<Guid, string?> _assetLinkGenerator;
    private readonly IAssetRepo _assetRepo;

    public InternalController(
        IAlbumRepo albumRepo, 
        ITrackRepo trackRepo, 
        IAssetRepo assetRepo,
        IOriginalTrackRepo originalTrackRepo, 
        IMapper mapper,
        LinkGenerator linkGenerator)
    {
        _albumRepo = albumRepo;
        _trackRepo = trackRepo;
        _assetRepo = assetRepo;
        _originalTrackRepo = originalTrackRepo;
        _mapper = mapper;

        // _linkGenerator = HttpContext.RequestServices.GetRequiredService<LinkGenerator>();
        _linkGenerator = linkGenerator;
        _assetLinkGenerator = assetId =>
            _linkGenerator.GetUriByName(HttpContext,
                nameof(AssetController.GetAsset),
                new { Id = assetId },
                fragment: FragmentString.Empty);
    }

    [DevelopmentOnly]
    [HttpPost("album/add/{albumId:Guid}")]
    public async Task<IActionResult> AddAlbum(Guid albumId, [FromBody] AlbumWriteDto albumWrite)
    {
        var album = _mapper.Map<AlbumWriteDto, Album>(albumWrite);
        album.Id = albumId;

        // get assets
        if (albumWrite.OtherImages != null)
        {
            var otherImages = await _assetRepo.GetAssetsById(albumWrite.OtherImages);
            album.OtherImages = otherImages.ToList();
        }

        if (albumWrite.AlbumImage != Guid.Empty && albumWrite.AlbumImage != null)
        {
            var albumThumb = await _assetRepo.GetAssetById((Guid)albumWrite.AlbumImage);
            album.AlbumImage = albumThumb;
        }

        var addedGuid = await _albumRepo.AddAlbum(album);

        await _albumRepo.SaveChanges();

        var addedAlbum = await _albumRepo.GetAlbum(addedGuid);

        return CreatedAtRoute(nameof(AlbumController.GetAlbum), 
            new { Id = addedGuid },
            addedAlbum);
    }

    [DevelopmentOnly]
    [HttpPost("album/{albumId:Guid}/track/add/{trackId:guid}")]
    public async Task<IActionResult> AddTrack(Guid albumId, Guid trackId, [FromBody] TrackWriteDto trackWrite)
    {
        var targetAlbum = await _albumRepo.GetAlbum(albumId);
        if (targetAlbum == null)
        {
            return NotFound();
        }

        // TODO: MAP Track Original Album to actual original albums
        if (trackWrite.Original?.Count > 0)
        {
            throw new NotImplementedException("Original Track is not yet implemented in Internal API");
        }
        
        var trackToAdd = _mapper.Map<TrackWriteDto, Track>(trackWrite);

        if (trackWrite.TrackFile != null && trackWrite.TrackFile != Guid.Empty)
        {
            var trackFile = await _assetRepo.GetAssetById((Guid)trackWrite.TrackFile);
            trackToAdd.TrackFile = trackFile;
        }

        trackToAdd.Id = trackId;
        var addedTrack = await _albumRepo.AddTrackToAlbum(albumId, trackToAdd);

        await _trackRepo.SaveChanges();

        return CreatedAtRoute(nameof(AlbumController.GetTrack), 
            new { Id = addedTrack.Id },
            new {Id = addedTrack.Id});
    }

    [DevelopmentOnly]
    [HttpPost("asset/add")]
    public async Task<IActionResult> AddAssetUnchecked([FromBody] Asset asset)
    {
        var addedId = await _assetRepo.AddAsset(asset);
        await _assetRepo.SaveChanges();
        var addedAsset = await _assetRepo.GetAssetById(addedId);

        if (addedAsset == null)
            throw new InvalidOperationException("Failed to Verify Transaction. Unable to retrieve newly added entry");

        return Ok(addedAsset);
    }
}