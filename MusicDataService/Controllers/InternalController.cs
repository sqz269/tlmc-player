using AutoMapper;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.JsonPatch;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Query.Internal;
using Microsoft.VisualBasic;
using MusicDataService.Data;
using MusicDataService.Data.Api;
using MusicDataService.Dtos.Album;
using MusicDataService.Dtos.Asset;
using MusicDataService.Dtos.Circle;
using MusicDataService.Dtos.Track;
using MusicDataService.Extensions;
using MusicDataService.Models;

namespace MusicDataService.Controllers;

// Controllers for Internal use only. All actions should have [DevelopmentOnly] Attribute
[ApiController]
[Route("api/internal")]
[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
public class InternalController : Controller
{
    private readonly IAlbumRepo _albumRepo;
    private readonly ITrackRepo _trackRepo;
    private readonly ICircleRepo _circleRepo;
    private readonly IOriginalTrackRepo _originalTrackRepo;
    private readonly AppDbContext _dbContext;
    private readonly IMapper _mapper;

    private readonly IAssetRepo _assetRepo;

    public InternalController(
        IAlbumRepo albumRepo, 
        ITrackRepo trackRepo, 
        ICircleRepo circleRepo,
        IAssetRepo assetRepo,
        IOriginalTrackRepo originalTrackRepo,
        AppDbContext dbContext,
        IMapper mapper)
    {
        _albumRepo = albumRepo;
        _trackRepo = trackRepo;
        _circleRepo = circleRepo;
        _assetRepo = assetRepo;
        _originalTrackRepo = originalTrackRepo;
        _dbContext = dbContext;
        _mapper = mapper;
    }

    [DevelopmentOnly]
    [HttpPut("album/add/{albumId:Guid}")]
    public async Task<IActionResult> AddAlbum(Guid albumId, [FromQuery] Guid? parentId, [FromBody] AlbumWriteDto albumWrite)
    {
        var a = await _dbContext.Albums.Where(a => a.Id == albumId).FirstOrDefaultAsync();
        if (a != null)
        {
            return Conflict($"Album with {albumId} already exists");
        }

        if (parentId != null)
        {
            var parent = await _dbContext.Albums.Where(p => p.Id == parentId).FirstOrDefaultAsync();
            if (parent == null)
            {
                return NotFound($"Parent album with id: {parentId} does not exist");
            }
            // need to save the current album entity before adding to the relation, so here we are just doing a sanity check
        }

        var album = _mapper.Map<AlbumWriteDto, Album>(albumWrite);
        album.Id = albumId;

        // get assets
        if (albumWrite.OtherFiles != null)
        {
            var otherImages = await _assetRepo.GetAssetsById(albumWrite.OtherFiles);
            album.OtherFiles = otherImages.ToList();
        }

        if (albumWrite.AlbumImage != Guid.Empty && albumWrite.AlbumImage != null)
        {
            var albumThumb = await _assetRepo.GetAssetById((Guid)albumWrite.AlbumImage);
            album.AlbumImage = albumThumb;
        }

        // get artists
        if (albumWrite.AlbumArtist != null && albumWrite.AlbumArtist.Count != 0)
        {
            var artists = await _circleRepo.GetCircles(albumWrite.AlbumArtist);
            album.AlbumArtist = artists.ToList();
        }

        var addedGuid = await _albumRepo.AddAlbum(album);

        await _albumRepo.SaveChanges();

        var addedAlbum = await _albumRepo.GetAlbum(addedGuid);

        if (parentId != null)
        {
            var parent = await _albumRepo.GetAlbum(parentId.Value);
            parent.ChildAlbums.Add(addedAlbum);
        }

        await _albumRepo.SaveChanges();

        return CreatedAtRoute(nameof(AlbumController.GetAlbum), 
            new { Id = addedGuid },
            addedAlbum);
    }

    [DevelopmentOnly]
    [HttpPut("album/{albumId:Guid}/track/add/{trackId:guid}")]
    public async Task<IActionResult> AddTrack(Guid albumId, Guid trackId, [FromBody] TrackWriteDto trackWrite)
    {
        var t = await _trackRepo.GetTrack(trackId);
        if (t != null)
        {
            return Conflict($"Track with {trackId} already exists");
        }

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
    [HttpPut("asset/add")]
    public async Task<IActionResult> AddAssetUnchecked([FromBody] Asset asset)
    {
        var a = await _assetRepo.GetAssetById(asset.AssetId);
        if (a != null)
        {
            return Conflict($"Asset with id: {asset.AssetId} already exists");
        }

        var addedId = await _assetRepo.AddAsset(asset);
        await _assetRepo.SaveChanges();
        var addedAsset = await _assetRepo.GetAssetById(addedId);

        if (addedAsset == null)
            throw new InvalidOperationException("Failed to Verify Transaction. Unable to retrieve newly added entry");

        return CreatedAtRoute(nameof(AssetController.GetAsset), new { Id = asset.AssetId }, new { Id = asset.AssetId });
    }


    [DevelopmentOnly]
    [HttpPatch("album/{albumId:guid}")]
    public async Task<IActionResult> UpdateAlbum(Guid albumId, [FromBody] JsonPatchDocument<AlbumUpdateDto> albumWrite)
    {
        var album = await _albumRepo.GetAlbum(albumId);

        var updatedAlbum = _mapper.Map<JsonPatchDocument<AlbumUpdateDto>, JsonPatchDocument<Album>>(albumWrite);

        updatedAlbum.ApplyTo(album);

        _dbContext.Albums.Update(album);

        await _albumRepo.SaveChanges();

        return Ok();
    }

    [DevelopmentOnly]
    [HttpPatch("track/{trackId:guid}")]
    public async Task<IActionResult> UpdateTrack(Guid trackId, [FromBody] TrackUpdateDto trackWrite)
    {
        var track = await _trackRepo.GetTrack(trackId);

        //var updatedTrack = _mapper.Map<JsonPatchDocument<TrackUpdateDto>, JsonPatchDocument<Track>>(trackWrite);
        //updatedTrack.ApplyTo(track);

        //_dbContext.Tracks.Update(track);
        //await _trackRepo.SaveChanges();
        //return Ok();

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

        var addedTrack = await _trackRepo.GetTrack(trackId);

        return Ok();
    }

    [DevelopmentOnly]
    [HttpPatch("track/jsonpatch/{trackId:guid}")]
    public async Task<IActionResult> UpdateTrack(Guid trackId, [FromBody] JsonPatchDocument<TrackUpdateDtoForJsonPatch> trackWrite)
    {
        var track = await _trackRepo.GetTrack(trackId);
        var updatedTrack = _mapper.Map<JsonPatchDocument<TrackUpdateDtoForJsonPatch>, JsonPatchDocument<Track>>(trackWrite);
        updatedTrack.ApplyTo(track);

        _dbContext.Tracks.Update(track);
        await _trackRepo.SaveChanges();
        return Ok();
    }

    [DevelopmentOnly]
    [HttpPut("circle/add/{id:Guid}")]
    public async Task<IActionResult> AddCircle(Guid id, [FromBody] CircleWriteDto circleWrite)
    {
        var circle = _mapper.Map<Circle>(circleWrite);
        circle.Id = id;

        var resultId = await _circleRepo.AddCircle(circle);
        await _circleRepo.SaveChanges();
        return Ok(resultId);
    }

    [DevelopmentOnly]
    [HttpPatch("circle/{id:guid}")]
    public async Task<IActionResult> UpdateCircle(Guid id, [FromBody] JsonPatchDocument<CircleUpdateDto> circleUpdate)
    {
        var circle = await _circleRepo.GetCircleById(id);

        var updatedCircle = _mapper.Map<JsonPatchDocument<CircleUpdateDto>, JsonPatchDocument<Circle>>(circleUpdate);

        updatedCircle.ApplyTo(circle);
        
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        await _circleRepo.SaveChanges();

        return Ok();
    }
}
