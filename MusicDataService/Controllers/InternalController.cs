using AutoMapper;
using Microsoft.AspNetCore.JsonPatch;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Query.Internal;
using Microsoft.VisualBasic;
using MusicDataService.Data;
using MusicDataService.Data.Api;
using MusicDataService.Dtos;
using MusicDataService.Extensions;
using MusicDataService.Models;

namespace MusicDataService.Controllers;

// Controllers for Internal use only. All actions should have [DevelopmentOnly] Attribute
[ApiController]
[Route("api/internal")]
[ApiExplorerSettings(IgnoreApi = true)]
public class InternalController : Controller
{
    private readonly IAlbumRepo _albumRepo;
    private readonly ITrackRepo _trackRepo;
    private readonly IOriginalTrackRepo _originalTrackRepo;
    private readonly IMapper _mapper;

    private readonly LinkGenerator _linkGenerator;
    private readonly IAssetRepo _assetRepo;

    public InternalController(
        IAlbumRepo albumRepo, 
        ITrackRepo trackRepo, 
        IAssetRepo assetRepo,
        IOriginalTrackRepo originalTrackRepo, 
        IMapper mapper)
    {
        _albumRepo = albumRepo;
        _trackRepo = trackRepo;
        _assetRepo = assetRepo;
        _originalTrackRepo = originalTrackRepo;
        _mapper = mapper;
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


    [DevelopmentOnly]
    [HttpPatch("album/{albumId:guid}")]
    public async Task<IActionResult> UpdateAlbum(Guid albumId, [FromBody] JsonPatchDocument<AlbumUpdateDto> albumWrite)
    {
        var album = await _albumRepo.GetAlbum(albumId);

        var updatedAlbum = _mapper.Map<JsonPatchDocument<AlbumUpdateDto>, JsonPatchDocument<Album>>(albumWrite);

        updatedAlbum.ApplyTo(album);

        await _albumRepo.SaveChanges();

        return Ok();
    }

    [DevelopmentOnly]
    [HttpPatch("track/{trackId:guid}")]
    public async Task<IActionResult> UpdateTrack(Guid trackId, [FromBody] TrackUpdateDto trackWrite)
    {
        var track = await _trackRepo.GetTrack(trackId);

        if (trackWrite.Original != null)
        {
            var originals = await _originalTrackRepo.GetOriginalTracks(trackWrite.Original);

            var originalTracks = originals as OriginalTrack[] ?? originals.ToArray();
            if (originalTracks.ToList().Count != trackWrite.Original.Count)
            {
                return BadRequest("Certain Original Track is Invalid");
            }

            track.Original.AddRange(originalTracks);

            //track.Original = track.Original.Distinct().ToList();
        }

        track.Genre.AddRange(trackWrite.Genre ?? new());
        //track.Genre = track.Genre.Distinct().ToList();
        track.Staff.AddRange(trackWrite.Staff ?? new());
        //track.Staff = track.Staff.Distinct().ToList();
        track.Arrangement.AddRange(trackWrite.Arrangement ?? new());
        //track.Arrangement = track.Arrangement.Distinct().ToList();
        track.Vocalist.AddRange(trackWrite.Vocalist ?? new());
        //track.Vocalist = track.Vocalist.Distinct().ToList();
        track.Lyricist.AddRange(trackWrite.Lyricist ?? new());
        //track.Lyricist = track.Lyricist.Distinct().ToList();
        track.OriginalNonTouhou = trackWrite.OriginalNonTouhou ?? track.OriginalNonTouhou;
        var saved = await _trackRepo.SaveChanges();

        Console.WriteLine($"Saved changes for: {track.Id} {saved}");

        //var addedTrack = await _trackRepo.GetTrack(trackId);

        return Ok();
    }
}
