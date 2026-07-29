using AutoMapper;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.JsonPatch;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TlmcPlayerBackend.Data;
using TlmcPlayerBackend.Data.Api.MusicData;
using TlmcPlayerBackend.Dtos.MusicData.Album;
using TlmcPlayerBackend.Dtos.MusicData.Asset;
using TlmcPlayerBackend.Dtos.MusicData.Circle;
using TlmcPlayerBackend.Dtos.MusicData.Hls;
using TlmcPlayerBackend.Dtos.MusicData.Track;
using TlmcPlayerBackend.Models.MusicData;
using TlmcPlayerBackend.Utils;
using TlmcPlayerBackend.Utils.Extensions;

namespace TlmcPlayerBackend.Controllers.MusicData;

// Controllers for Internal use only. All actions should have [InternalApiKey] Attribute
[ApiController]
[Route("api/internal")]
public class InternalController : Controller
{
    private readonly IAlbumRepo _albumRepo;
    private readonly ITrackRepo _trackRepo;
    private readonly ICircleRepo _circleRepo;
    private readonly IOriginalTrackRepo _originalTrackRepo;
    private readonly AppDbContext _dbContext;
    private readonly IMapper _mapper;

    private readonly IAssetRepo _assetRepo;
    private readonly AssetPathPolicy _assetPathPolicy;

    public InternalController(
        IAlbumRepo albumRepo, 
        ITrackRepo trackRepo, 
        ICircleRepo circleRepo,
        IAssetRepo assetRepo,
        IOriginalTrackRepo originalTrackRepo,
        AppDbContext dbContext,
        AssetPathPolicy assetPathPolicy,
        IMapper mapper)
    {
        _assetPathPolicy = assetPathPolicy;
        _albumRepo = albumRepo;
        _trackRepo = trackRepo;
        _circleRepo = circleRepo;
        _assetRepo = assetRepo;
        _originalTrackRepo = originalTrackRepo;
        _dbContext = dbContext;
        _mapper = mapper;
    }

    [InternalApiKey]
    [HttpPut("album/add/{albumId:Guid}", Name = $"___INTERNAL_{nameof(AddAlbum)}")]
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
            album.Image = albumThumb;
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

    [InternalApiKey]
    [HttpPut("album/{albumId:Guid}/track/add/{trackId:guid}", Name = $"___INTERNAL_{nameof(AddTrack)}")]
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

        return CreatedAtRoute(nameof(TrackController.GetTrack), 
            new { Id = addedTrack.Id },
            new {Id = addedTrack.Id});
    }

    [InternalApiKey]
    [HttpPut("track/{trackId:Guid}/lyrics/add/{lyricsId:Guid}", Name = $"___INTERNAL_{nameof(AddLyrics)}")]
    public async Task<IActionResult> AddLyrics(Guid trackId, Guid lyricsId, [FromBody] Lyrics lyrics)
    {
        var result = await _trackRepo.PutTrackLyrics(lyricsId, trackId, lyrics);
        if (result != null)
        {
            return Ok(result); 
        }

        return Problem("Failed to put, check log for problem");
    }

    [InternalApiKey]
    [HttpPut("asset/add", Name = $"___INTERNAL_{nameof(AddAssetUnchecked)}")]
    public async Task<IActionResult> AddAssetUnchecked([FromBody] AssetWriteDto assetWrite)
    {
        // Path is stored and later opened directly by AssetController, so it is
        // validated here rather than on the way out only.
        if (!_assetPathPolicy.IsAllowed(assetWrite.Path))
        {
            return BadRequest(_assetPathPolicy.IsConstrained
                ? "Asset path is outside the permitted asset roots"
                : "Asset path must be an absolute path without traversal segments");
        }

        var a = await _assetRepo.GetAssetById(assetWrite.Id);
        if (a != null)
        {
            return Conflict($"Asset with id: {assetWrite.Id} already exists");
        }

        var asset = _mapper.Map<Asset>(assetWrite);
        var addedId = await _assetRepo.AddAsset(asset);
        await _assetRepo.SaveChanges();
        var addedAsset = await _assetRepo.GetAssetById(addedId);

        if (addedAsset == null)
            throw new InvalidOperationException("Failed to Verify Transaction. Unable to retrieve newly added entry");

        return CreatedAtRoute(nameof(AssetController.GetAsset), new { Id = addedAsset.Id }, new { Id = addedAsset.Id });
    }

    [InternalApiKey]
    [HttpPut("asset/track/{trackId:guid}/segment", Name = $"___INTERNAL_{nameof(AddHlsFileSegment)}")]
    public async Task<IActionResult> AddHlsFileSegment(Guid trackId, [FromQuery] int quality, [FromBody] HlsSegmentWriteDto segmentWrite)
    {
        if (segmentWrite.Id == Guid.Empty)
            segmentWrite.Id = Guid.NewGuid();
        
        // get the playlist for segment
        var playlist = await _dbContext.HlsPlaylist.Where(p => p.TrackId == trackId && p.Bitrate == quality).FirstOrDefaultAsync();

        if (playlist == null)
        {
            return BadRequest("Playlist does not exist for this track and quality");
        }

        var segment = _mapper.Map<HlsSegmentWriteDto, HlsSegment>(segmentWrite);

        segment.HlsPlaylist = playlist;

        _dbContext.HlsSegment.Add(segment);
        await _dbContext.SaveChangesAsync();
        return Ok();
    }
    
    [InternalApiKey]
    [HttpPut("asset/track/{trackId:guid}/playlist", Name = $"___INTERNAL_{nameof(AddHlsFilePlaylist)}")]
    public async Task<IActionResult> AddHlsFilePlaylist([FromBody] HlsPlaylistWriteDto playlistWrite)
    {
        if (playlistWrite.Id == Guid.Empty)
            playlistWrite.Id = Guid.NewGuid();

        // ensure the track exists
        var track = await _dbContext.Tracks.Where(t => t.Id == playlistWrite.TrackId).FirstOrDefaultAsync();
        if (track == null)
        {
            return BadRequest("Track does not exist");
        }

        var playlist = _mapper.Map<HlsPlaylistWriteDto, HlsPlaylist>(playlistWrite);
        
        playlist.Track = track;

        _dbContext.HlsPlaylist.Add(playlist);
        await _dbContext.SaveChangesAsync();
        return Ok();
    }

    [InternalApiKey]
    [HttpPatch("album/{albumId:guid}", Name = $"___INTERNAL_{nameof(UpdateAlbum)}")]
    public async Task<IActionResult> UpdateAlbum(Guid albumId, [FromBody] JsonPatchDocument<AlbumUpdateDto> albumWrite)
    {
        var album = await _albumRepo.GetAlbum(albumId);
        if (album == null)
        {
            return NotFound($"No album with id: {albumId} exists");
        }

        var updatedAlbum = _mapper.Map<JsonPatchDocument<AlbumUpdateDto>, JsonPatchDocument<Album>>(albumWrite);

        // ModelState overload: an unresolvable "path" becomes a 400 instead of an
        // unhandled JsonPatchException.
        updatedAlbum.ApplyTo(album, ModelState);
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        _dbContext.Albums.Update(album);

        await _albumRepo.SaveChanges();

        return Ok();
    }

    [InternalApiKey]
    [HttpPatch("track/{trackId:guid}", Name = $"___INTERNAL_{nameof(UpdateTrack)}")]
    public async Task<IActionResult> UpdateTrack(Guid trackId, [FromBody] TrackUpdateDto trackWrite)
    {
        var track = await _trackRepo.GetTrack(trackId);
        if (track == null)
        {
            return NotFound($"No track with id: {trackId} exists");
        }

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

    [InternalApiKey]
    [HttpPatch("track/jsonpatch/{trackId:guid}", Name = $"___INTERNAL_PATCH_{nameof(UpdateTrack)}")]
    public async Task<IActionResult> UpdateTrack(Guid trackId, [FromBody] JsonPatchDocument<TrackUpdateDtoForJsonPatch> trackWrite)
    {
        var track = await _trackRepo.GetTrack(trackId);
        if (track == null)
        {
            return NotFound($"No track with id: {trackId} exists");
        }

        var updatedTrack = _mapper.Map<JsonPatchDocument<TrackUpdateDtoForJsonPatch>, JsonPatchDocument<Track>>(trackWrite);
        updatedTrack.ApplyTo(track, ModelState);
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        _dbContext.Tracks.Update(track);
        await _trackRepo.SaveChanges();
        return Ok();
    }

    [InternalApiKey]
    [HttpPut("circle/add/{id:Guid}", Name = $"___INTERNAL_{nameof(AddCircle)}")]
    public async Task<IActionResult> AddCircle(Guid id, [FromBody] CircleWriteDto circleWrite)
    {
        var circle = _mapper.Map<Circle>(circleWrite);
        circle.Id = id;

        var resultId = await _circleRepo.AddCircle(circle);
        await _circleRepo.SaveChanges();
        return Ok(resultId);
    }

    [InternalApiKey]
    [HttpPatch("circle/{id:guid}", Name = $"___INTERNAL_{nameof(UpdateCircle)}")]
    public async Task<IActionResult> UpdateCircle(Guid id, [FromBody] JsonPatchDocument<CircleUpdateDto> circleUpdate)
    {
        var circle = await _circleRepo.GetCircleById(id);
        if (circle == null)
        {
            return NotFound($"No circle with id: {id} exists");
        }

        var updatedCircle = _mapper.Map<JsonPatchDocument<CircleUpdateDto>, JsonPatchDocument<Circle>>(circleUpdate);

        updatedCircle.ApplyTo(circle, ModelState);
        
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        await _circleRepo.SaveChanges();

        return Ok();
    }
}
