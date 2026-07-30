using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using TlmcPlayerBackend.Data;
using TlmcPlayerBackend.Data.Repos;
using TlmcPlayerBackend.Ids;
using TlmcPlayerBackend.Models.MusicData;
using TlmcPlayerBackend.Search;
using TlmcPlayerBackend.Utils;

namespace TlmcPlayerBackend.Subsonic;

/// <summary>
/// The OpenSubsonic facade (Docs/SUBSONIC.md). Deliberately not [ApiController]:
/// errors here are protocol-level (HTTP 200, status="failed"), so model binding
/// must not answer 400 on its own. Every method maps twice — pre-1.14 clients
/// append ".view" — and accepts POST for the formPost extension.
/// Kept out of the v1 OpenAPI document — the contract is the Subsonic spec,
/// and generated native clients must not bind to it — but published as its own
/// browsing-aid document (SubsonicSwagger).
/// </summary>
[Route("rest")]
[ServiceFilter(typeof(SubsonicAuthFilter))]
[ApiExplorerSettings(GroupName = SubsonicSwagger.DocName)]
public class SubsonicController(
    AppDbContext context,
    SubsonicQueries queries,
    IReleaseRepo releaseRepo,
    ITrackRepo trackRepo,
    ISimilarityRepo similarityRepo,
    ISearchIndexService search,
    StorageRootResolver resolver,
    IOptions<SubsonicOptions> options) : ControllerBase
{
    private const int MaxPageSize = 500;

    private readonly AppDbContext _context = context;
    private readonly SubsonicQueries _queries = queries;
    private readonly IReleaseRepo _releaseRepo = releaseRepo;
    private readonly ITrackRepo _trackRepo = trackRepo;
    private readonly ISimilarityRepo _similarityRepo = similarityRepo;
    private readonly ISearchIndexService _search = search;
    private readonly StorageRootResolver _resolver = resolver;
    private readonly SubsonicOptions _options = options.Value;

    // -- System ----------------------------------------------------------------

    [AcceptVerbs("GET", "POST", Route = "ping")]
    [AcceptVerbs("GET", "POST", Route = "ping.view")]
    public IActionResult Ping() => SubsonicResult.Ok();

    [AcceptVerbs("GET", "POST", Route = "getLicense")]
    [AcceptVerbs("GET", "POST", Route = "getLicense.view")]
    public IActionResult GetLicense() => SubsonicResult.Ok(e => e.License = new LicenseDto());

    [SubsonicNoAuth]
    [AcceptVerbs("GET", "POST", Route = "getOpenSubsonicExtensions")]
    [AcceptVerbs("GET", "POST", Route = "getOpenSubsonicExtensions.view")]
    public IActionResult GetOpenSubsonicExtensions() => SubsonicResult.Ok(e =>
        e.OpenSubsonicExtensions =
        [
            new OpenSubsonicExtensionDto { Name = "apiKeyAuthentication", Versions = [1] },
            new OpenSubsonicExtensionDto { Name = "formPost", Versions = [1] },
        ]);

    [AcceptVerbs("GET", "POST", Route = "getMusicFolders")]
    [AcceptVerbs("GET", "POST", Route = "getMusicFolders.view")]
    public IActionResult GetMusicFolders() => SubsonicResult.Ok(e =>
        e.MusicFolders = new MusicFoldersDto
        {
            MusicFolder = [new MusicFolderDto { Id = 1, Name = "TLMC" }],
        });

    // -- Browsing (ID3) --------------------------------------------------------

    [AcceptVerbs("GET", "POST", Route = "getArtists")]
    [AcceptVerbs("GET", "POST", Route = "getArtists.view")]
    public async Task<IActionResult> GetArtists()
    {
        var rows = await _queries.GetArtistIndex();

        // Rows arrive ordered by lower(name); bucket by first character, letters
        // A–Z then a single '#' bucket for everything else (digits, CJK, symbols).
        var buckets = rows
            .GroupBy(r => char.IsAsciiLetter(r.Name.FirstOrDefault())
                ? char.ToUpperInvariant(r.Name[0]).ToString()
                : "#")
            .OrderBy(g => g.Key == "#" ? 1 : 0)
            .ThenBy(g => g.Key, StringComparer.Ordinal)
            .Select(g => new ArtistIndexDto
            {
                Name = g.Key,
                Artist = g.Select(SubsonicMapper.ToArtist).ToList(),
            })
            .ToList();

        return SubsonicResult.Ok(e => e.Artists = new ArtistsDto { Index = buckets });
    }

    [AcceptVerbs("GET", "POST", Route = "getArtist")]
    [AcceptVerbs("GET", "POST", Route = "getArtist.view")]
    public async Task<IActionResult> GetArtist(string? id)
    {
        if (!CircleId.TryParse(id, null, out var circleId))
        {
            return NotFoundError();
        }

        var artist = await _queries.GetArtist(circleId);
        if (artist == null)
        {
            return NotFoundError();
        }

        var albums = await _queries.GetAlbumsByCircle(circleId);
        return SubsonicResult.Ok(e => e.Artist = SubsonicMapper.ToArtistWithAlbums(artist, albums));
    }

    [AcceptVerbs("GET", "POST", Route = "getAlbum")]
    [AcceptVerbs("GET", "POST", Route = "getAlbum.view")]
    public async Task<IActionResult> GetAlbum(string? id)
    {
        if (!ReleaseId.TryParse(id, null, out var releaseId))
        {
            return NotFoundError();
        }

        var release = await _releaseRepo.GetRelease(releaseId);
        if (release == null)
        {
            return NotFoundError();
        }

        // ReleaseReadDto has no created_at; the `created` attribute is required.
        var createdAt = await _context.Releases.AsNoTracking()
            .Where(r => r.Id == releaseId)
            .Select(r => r.CreatedAt)
            .FirstAsync();

        return SubsonicResult.Ok(e =>
            e.Album = SubsonicMapper.ToAlbumWithSongs(release, createdAt, _options.ServeLossless));
    }

    [AcceptVerbs("GET", "POST", Route = "getSong")]
    [AcceptVerbs("GET", "POST", Route = "getSong.view")]
    public async Task<IActionResult> GetSong(string? id)
    {
        if (!TrackId.TryParse(id, null, out var trackId))
        {
            return NotFoundError();
        }

        var track = await _trackRepo.GetTrack(trackId);
        if (track == null)
        {
            return NotFoundError();
        }

        return SubsonicResult.Ok(e => e.Song = SubsonicMapper.ToSong(track, _options.ServeLossless));
    }

    // -- Lists -----------------------------------------------------------------

    [AcceptVerbs("GET", "POST", Route = "getAlbumList2")]
    [AcceptVerbs("GET", "POST", Route = "getAlbumList2.view")]
    public async Task<IActionResult> GetAlbumList2(
        string? type, int size = 10, int offset = 0, int fromYear = -1, int toYear = -1)
    {
        if (string.IsNullOrEmpty(type))
        {
            return SubsonicResult.Error(SubsonicErrorCodes.MissingParameter,
                "Required parameter is missing: type");
        }

        AlbumListType? listType = type switch
        {
            "alphabeticalByName" => AlbumListType.AlphabeticalByName,
            "newest" => AlbumListType.Newest,
            "byYear" => AlbumListType.ByYear,
            "random" => AlbumListType.Random,
            _ => null,
        };
        if (listType == null)
        {
            return SubsonicResult.Error(SubsonicErrorCodes.NotImplemented,
                $"Album list type '{type}' is not supported yet");
        }

        if (listType == AlbumListType.ByYear && (fromYear < 0 || toYear < 0))
        {
            return SubsonicResult.Error(SubsonicErrorCodes.MissingParameter,
                "byYear requires fromYear and toYear");
        }

        var rows = await _queries.GetAlbumList(
            listType.Value, Math.Clamp(size, 1, MaxPageSize), Math.Max(offset, 0), fromYear, toYear);

        return SubsonicResult.Ok(e => e.AlbumList2 = new AlbumList2Dto
        {
            Album = rows.Select(SubsonicMapper.ToAlbum).ToList(),
        });
    }

    [AcceptVerbs("GET", "POST", Route = "getRandomSongs")]
    [AcceptVerbs("GET", "POST", Route = "getRandomSongs.view")]
    public async Task<IActionResult> GetRandomSongs(int size = 10)
    {
        var tracks = await _trackRepo.GetRandom(Math.Clamp(size, 1, 100), seed: null);
        return SubsonicResult.Ok(e => e.RandomSongs = new SongsDto
        {
            Song = tracks.Select(t => SubsonicMapper.ToSong(t, _options.ServeLossless)).ToList(),
        });
    }

    // -- Search ----------------------------------------------------------------

    [AcceptVerbs("GET", "POST", Route = "search3")]
    [AcceptVerbs("GET", "POST", Route = "search3.view")]
    public async Task<IActionResult> Search3(
        string? query, int artistCount = 20, int artistOffset = 0,
        int albumCount = 20, int albumOffset = 0, int songCount = 20, int songOffset = 0)
    {
        if (query == null && !HasQueryParameter())
        {
            return SubsonicResult.Error(SubsonicErrorCodes.MissingParameter,
                "Required parameter is missing: query");
        }

        var (artists, albums, songs) = await Search(
            query ?? "", artistCount, artistOffset, albumCount, albumOffset, songCount, songOffset);

        return SubsonicResult.Ok(e => e.SearchResult3 = new SearchResult3Dto
        {
            Artist = artists.Select(SubsonicMapper.ToArtist).ToList(),
            Album = albums.Select(SubsonicMapper.ToAlbum).ToList(),
            Song = songs.Select(t => SubsonicMapper.ToSong(t, _options.ServeLossless)).ToList(),
        });
    }

    [AcceptVerbs("GET", "POST", Route = "search2")]
    [AcceptVerbs("GET", "POST", Route = "search2.view")]
    public async Task<IActionResult> Search2(
        string? query, int artistCount = 20, int artistOffset = 0,
        int albumCount = 20, int albumOffset = 0, int songCount = 20, int songOffset = 0)
    {
        if (query == null && !HasQueryParameter())
        {
            return SubsonicResult.Error(SubsonicErrorCodes.MissingParameter,
                "Required parameter is missing: query");
        }

        var (artists, albums, songs) = await Search(
            query ?? "", artistCount, artistOffset, albumCount, albumOffset, songCount, songOffset);

        return SubsonicResult.Ok(e => e.SearchResult2 = new SearchResult2Dto
        {
            Artist = artists
                .Select(a => new ArtistDto { Id = new CircleId(a.Id).ToString(), Name = a.Name })
                .ToList(),
            Album = albums.Select(SubsonicMapper.ToAlbumChild).ToList(),
            Song = songs.Select(t => SubsonicMapper.ToSong(t, _options.ServeLossless)).ToList(),
        });
    }

    /// <summary>
    /// MVC binds `?query=` (present but empty — the full-library-sync idiom) to
    /// null, same as an absent parameter; only the raw request can tell them apart.
    /// </summary>
    private bool HasQueryParameter()
        => Request.Query.ContainsKey("query")
           || (Request.HasFormContentType && Request.Form.ContainsKey("query"));

    private async Task<(List<ArtistRow>, List<AlbumRow>, List<TrackWithContext>)> Search(
        string query, int artistCount, int artistOffset,
        int albumCount, int albumOffset, int songCount, int songOffset)
    {
        query = query.Trim();
        artistCount = Math.Clamp(artistCount, 0, MaxPageSize);
        albumCount = Math.Clamp(albumCount, 0, MaxPageSize);
        songCount = Math.Clamp(songCount, 0, MaxPageSize);
        artistOffset = Math.Max(artistOffset, 0);
        albumOffset = Math.Max(albumOffset, 0);
        songOffset = Math.Max(songOffset, 0);

        var artists = artistCount == 0
            ? []
            : await _queries.SearchArtists(query, artistCount, artistOffset);
        var albums = albumCount == 0
            ? []
            : await _queries.SearchAlbums(query, albumCount, albumOffset);
        var songs = songCount == 0
            ? []
            : await SearchSongs(query, songCount, songOffset);

        return (artists, albums, songs);
    }

    /// <summary>
    /// Songs come from Meilisearch when it can answer, Postgres otherwise. The
    /// empty query — the full-library sync idiom — always comes from Postgres:
    /// deterministic order, never a stale index. A failed engine degrades to the
    /// substring path instead of erroring; clients treat search failures as
    /// "no results" and stop there.
    /// </summary>
    private async Task<List<TrackWithContext>> SearchSongs(string query, int count, int offset)
    {
        if (query.Length == 0 || !_search.Enabled)
        {
            return await _queries.SearchSongsInPostgres(query, count, offset);
        }

        try
        {
            var result = await _search.SearchAsync(query, count, offset, locales: null, HttpContext.RequestAborted);
            return await _trackRepo.GetWithContext(result.Hits.Select(h => h.Id).ToList());
        }
        catch (MeiliUnavailableException)
        {
            return await _queries.SearchSongsInPostgres(query, count, offset);
        }
    }

    // -- Media -----------------------------------------------------------------

    [AcceptVerbs("GET", "POST", Route = "getCoverArt")]
    [AcceptVerbs("GET", "POST", Route = "getCoverArt.view")]
    public async Task<IActionResult> GetCoverArt(string? id, int size = 0)
    {
        var artworkId = await ResolveArtworkId(id);
        if (artworkId == null)
        {
            return NotFoundError();
        }

        var artwork = await _context.Artworks.AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == artworkId);
        if (artwork == null)
        {
            return NotFoundError();
        }

        // Variant ladder: 0 is the unresized original. Requested size → smallest
        // variant at least that big, else the original; no size → the original.
        // No variants at all (the resize stage hasn't run) → the source image:
        // oversized beats a broken cover, and clients downscale anyway.
        var original = artwork.Variants.FirstOrDefault(v => v.SizePx == 0);
        var chosen = size > 0
            ? artwork.Variants
                  .Where(v => v.SizePx >= size)
                  .OrderBy(v => v.SizePx)
                  .FirstOrDefault() ?? original
            : original;
        chosen ??= artwork.Variants.OrderByDescending(v => v.SizePx).FirstOrDefault();

        return await ServeAsset(chosen?.AssetId ?? artwork.SourceAssetId, "image/jpeg");
    }

    [AcceptVerbs("GET", "POST", Route = "stream")]
    [AcceptVerbs("GET", "POST", Route = "stream.view")]
    public Task<IActionResult> Stream(string? id, int maxBitRate = 0, string? format = null)
        => ServeTrackAudio(id, maxBitRate, format, download: false);

    [AcceptVerbs("GET", "POST", Route = "download")]
    [AcceptVerbs("GET", "POST", Route = "download.view")]
    public Task<IActionResult> Download(string? id)
        => ServeTrackAudio(id, maxBitRate: 0, format: null, download: true);

    /// <summary>
    /// Docs/SUBSONIC.md section 5: the pre-transcoded AAC rungs are the default
    /// and only rendition — each rung file is a self-contained fMP4, valid as a
    /// progressive audio/mp4 stream — with the source FLAC behind the
    /// ServeLossless opt-in. `format` is a preference, not a contract.
    /// </summary>
    private async Task<IActionResult> ServeTrackAudio(string? id, int maxBitRate, string? format, bool download)
    {
        if (!TrackId.TryParse(id, null, out var trackId))
        {
            return NotFoundError();
        }

        var track = await _context.Tracks.AsNoTracking()
            .Where(t => t.Id == trackId)
            .Select(t => new { t.Name, t.MediaKey, t.HlsBitrates, t.SourceAssetId })
            .FirstOrDefaultAsync();
        if (track == null)
        {
            return NotFoundError();
        }

        var wantsLossless = _options.ServeLossless
            && (download || maxBitRate <= 0 || format is "raw" or "flac");

        if (wantsLossless && track.SourceAssetId is { } flacAsset)
        {
            return await ServeAsset(flacAsset, "audio/flac",
                download ? $"{SafeFileName(track.Name.Default)}.flac" : null);
        }

        if (track.MediaKey == null || track.HlsBitrates.Count == 0)
        {
            return NotFoundError();
        }

        var rung = maxBitRate > 0 && !download
            ? track.HlsBitrates.Where(b => b <= maxBitRate).DefaultIfEmpty(track.HlsBitrates.Min()).Max()
            : track.HlsBitrates.Max();

        var path = _resolver.Resolve(StorageRoot.Library, track.MediaKey, "hls", $"{rung}k", "stream.m4s");
        if (path == null || !System.IO.File.Exists(path))
        {
            return NotFoundError();
        }

        return download
            ? PhysicalFile(path, "audio/mp4", $"{SafeFileName(track.Name.Default)}.m4a", enableRangeProcessing: true)
            : PhysicalFile(path, "audio/mp4", enableRangeProcessing: true);
    }

    // -- Phase-2 surfaces, stubbed empty ---------------------------------------
    // Clients query these on startup; an empty section renders, an error 30
    // toasts. Real implementations arrive with phase 2 (Docs/SUBSONIC.md §9).

    [AcceptVerbs("GET", "POST", Route = "getPlaylists")]
    [AcceptVerbs("GET", "POST", Route = "getPlaylists.view")]
    public IActionResult GetPlaylists() => SubsonicResult.Ok(e => e.Playlists = new PlaylistsDto());

    [AcceptVerbs("GET", "POST", Route = "getStarred2")]
    [AcceptVerbs("GET", "POST", Route = "getStarred2.view")]
    public IActionResult GetStarred2() => SubsonicResult.Ok(e => e.Starred2 = new Starred2Dto());

    [AcceptVerbs("GET", "POST", Route = "getGenres")]
    [AcceptVerbs("GET", "POST", Route = "getGenres.view")]
    public IActionResult GetGenres() => SubsonicResult.Ok(e => e.Genres = new GenresDto());

    [AcceptVerbs("GET", "POST", Route = "getInternetRadioStations")]
    [AcceptVerbs("GET", "POST", Route = "getInternetRadioStations.view")]
    public IActionResult GetInternetRadioStations()
        => SubsonicResult.Ok(e => e.InternetRadioStations = new InternetRadioStationsDto());

    // -- Similarity and discovery (Docs/SUBSONIC.md section 10) ----------------

    [AcceptVerbs("GET", "POST", Route = "getSimilarSongs2")]
    [AcceptVerbs("GET", "POST", Route = "getSimilarSongs2.view")]
    public async Task<IActionResult> GetSimilarSongs2(string? id, int count = 50)
    {
        var songs = await SimilarSongsFor(id, count);
        return songs == null
            ? NotFoundError()
            : SubsonicResult.Ok(e => e.SimilarSongs2 = new SimilarSongsDto { Song = songs });
    }

    [AcceptVerbs("GET", "POST", Route = "getSimilarSongs")]
    [AcceptVerbs("GET", "POST", Route = "getSimilarSongs.view")]
    public async Task<IActionResult> GetSimilarSongs(string? id, int count = 50)
    {
        var songs = await SimilarSongsFor(id, count);
        return songs == null
            ? NotFoundError()
            : SubsonicResult.Ok(e => e.SimilarSongs = new SimilarSongsDto { Song = songs });
    }

    [AcceptVerbs("GET", "POST", Route = "getArtistInfo2")]
    [AcceptVerbs("GET", "POST", Route = "getArtistInfo2.view")]
    public async Task<IActionResult> GetArtistInfo2(string? id, int count = 20)
    {
        if (!CircleId.TryParse(id, null, out var circleId)
            || !await _context.Circles.AsNoTracking().AnyAsync(c => c.Id == circleId))
        {
            return NotFoundError();
        }

        // similar_circle in style order — the offline materialization SUBSONIC.md
        // section 10 reserved; no aggregation over similar_track at query time.
        var rows = await _context.SimilarCircles.AsNoTracking()
            .Where(s => s.AnchorCircleId == circleId && s.RankStyle != null)
            .OrderBy(s => s.RankStyle)
            .Take(Math.Clamp(count, 1, 100))
            .Select(s => new ArtistRow
            {
                Id = s.NeighborCircleId.Value,
                Name = s.NeighborCircle.Name,
                AlbumCount = s.NeighborCircle.Releases.Count,
            })
            .ToListAsync();

        return SubsonicResult.Ok(e => e.ArtistInfo2 = new ArtistInfo2Dto
        {
            SimilarArtist = rows.Select(SubsonicMapper.ToArtist).ToList(),
        });
    }

    /// <summary>
    /// Songs similar to a track, release or circle. Null means the anchor does not
    /// exist (protocol NotFound); an empty list means it exists but similarity has
    /// nothing to say (no embedded media) — clients treat that as routine.
    /// </summary>
    private async Task<List<ChildDto>?> SimilarSongsFor(string? id, int count)
    {
        count = Math.Clamp(count, 1, 100);
        if (TrackId.TryParse(id, null, out var trackId))
        {
            return await SimilarToTrack(trackId, count);
        }

        if (ReleaseId.TryParse(id, null, out var releaseId))
        {
            return await SimilarToRelease(releaseId, count);
        }

        if (CircleId.TryParse(id, null, out var circleId))
        {
            return await SimilarToCircle(circleId, count);
        }

        return null;
    }

    /// <summary>Native /similar verbatim: precomputed ranks, ANN fallback, diversified.</summary>
    private async Task<List<ChildDto>?> SimilarToTrack(TrackId trackId, int count)
    {
        var overFetch = Math.Min(100, count * 3);
        var candidates = await _similarityRepo.GetPrecomputed(trackId, overFetch);
        if (candidates.Count == 0)
        {
            if (await _trackRepo.GetTrack(trackId) == null)
            {
                return null;
            }

            try
            {
                candidates = await _similarityRepo.GetApproximate(trackId, overFetch);
            }
            catch (EmbeddingNotFoundException)
            {
                return [];
            }
        }

        // >= ~0.999 chamfer is the same recording on another release (SCHEMA-V6.md's
        // duplicate band): an instant mix must not open with a bit-identical copy of
        // its anchor. Copies of *each other* deeper in the list can still slip
        // through — there are no candidate-to-candidate scores to catch them with.
        candidates = candidates.Where(c => c.Score < 0.9985f).ToList();

        var reranked = DiversityReranker.Rerank(
            candidates.Select(c => new DiversityReranker.Candidate(
                c.Context.Track.Id,
                c.Score,
                c.Context.Release.Id,
                c.Context.Circles.Select(x => x.Id).ToList())).ToList(),
            count);

        var byId = candidates.ToDictionary(c => c.Context.Track.Id);
        return reranked
            .Select(r => SubsonicMapper.ToSong(byId[r.Candidate.TrackId].Context, _options.ServeLossless))
            .ToList();
    }

    /// <summary>
    /// similar_release in style order (shared recordings already demoted), then
    /// songs drawn round-robin across the neighbor releases — diversity across
    /// releases is structural, so no reranker pass.
    /// </summary>
    private async Task<List<ChildDto>?> SimilarToRelease(ReleaseId releaseId, int count)
    {
        var neighbors = await _context.SimilarReleases.AsNoTracking()
            .Where(s => s.AnchorReleaseId == releaseId && s.RankStyle != null)
            .OrderBy(s => s.RankStyle)
            .Take(count)
            .Select(s => s.NeighborReleaseId)
            .ToListAsync();

        if (neighbors.Count == 0)
        {
            return await _context.Releases.AsNoTracking().AnyAsync(r => r.Id == releaseId)
                ? []
                : null;
        }

        return await RoundRobinSongs(neighbors, count);
    }

    /// <summary>
    /// similar_circle in style order; each neighbor circle contributes releases in
    /// date order, interleaved circle-by-circle, minus collabs with the anchor
    /// (its own tracks must not come back as "similar").
    /// </summary>
    private async Task<List<ChildDto>?> SimilarToCircle(CircleId circleId, int count)
    {
        var neighbors = await _context.SimilarCircles.AsNoTracking()
            .Where(s => s.AnchorCircleId == circleId && s.RankStyle != null)
            .OrderBy(s => s.RankStyle)
            .Take(count)
            .Select(s => s.NeighborCircleId)
            .ToListAsync();

        if (neighbors.Count == 0)
        {
            return await _context.Circles.AsNoTracking().AnyAsync(c => c.Id == circleId)
                ? []
                : null;
        }

        var releaseRows = await _context.ReleaseCircles.AsNoTracking()
            .Where(rc => neighbors.Contains(rc.CircleId)
                         && !rc.Release.Circles.Any(other => other.CircleId == circleId))
            .Select(rc => new { rc.CircleId, rc.ReleaseId, rc.Release.ReleaseDate })
            .ToListAsync();

        var byCircle = releaseRows
            .GroupBy(r => r.CircleId)
            .ToDictionary(
                g => g.Key,
                g => g.OrderByDescending(r => r.ReleaseDate ?? DateOnly.MinValue)
                    .Select(r => r.ReleaseId)
                    .ToList());

        var releases = new List<ReleaseId>();
        for (var pass = 0; releases.Count < count; pass++)
        {
            var advanced = false;
            foreach (var circle in neighbors)
            {
                if (byCircle.TryGetValue(circle, out var list) && pass < list.Count)
                {
                    releases.Add(list[pass]);
                    advanced = true;
                    if (releases.Count >= count)
                    {
                        break;
                    }
                }
            }

            if (!advanced)
            {
                break;
            }
        }

        return await RoundRobinSongs(releases, count);
    }

    /// <summary>
    /// One song per release per pass, releases kept in similarity-rank order,
    /// tracks within a release in disc/track order, unstreamable tracks skipped.
    /// </summary>
    private async Task<List<ChildDto>> RoundRobinSongs(List<ReleaseId> releasesInRankOrder, int count)
    {
        var trackRows = await _context.Tracks.AsNoTracking()
            .Where(t => releasesInRankOrder.Contains(t.Disc.ReleaseId) && t.MediaKey != null)
            .Select(t => new
            {
                t.Id,
                t.Disc.ReleaseId,
                t.Disc.DiscNumber,
                t.TrackNumber,
            })
            .ToListAsync();

        var byRelease = trackRows
            .GroupBy(t => t.ReleaseId)
            .ToDictionary(
                g => g.Key,
                g => g.OrderBy(t => t.DiscNumber).ThenBy(t => t.TrackNumber)
                    .Select(t => t.Id)
                    .ToList());

        var picked = new List<TrackId>();
        for (var pass = 0; picked.Count < count; pass++)
        {
            var advanced = false;
            foreach (var release in releasesInRankOrder)
            {
                if (byRelease.TryGetValue(release, out var list) && pass < list.Count)
                {
                    picked.Add(list[pass]);
                    advanced = true;
                    if (picked.Count >= count)
                    {
                        break;
                    }
                }
            }

            if (!advanced)
            {
                break;
            }
        }

        var contexts = await _trackRepo.GetWithContext(picked);
        var byId = contexts.ToDictionary(c => c.Track.Id);
        return picked
            .Where(byId.ContainsKey)
            .Select(p => SubsonicMapper.ToSong(byId[p], _options.ServeLossless))
            .ToList();
    }

    // -- Everything else -------------------------------------------------------

    /// <summary>
    /// Unknown /rest methods answer the protocol's "not implemented", not an
    /// HTML 404 — clients show the message instead of choking on the body.
    /// </summary>
    [AcceptVerbs("GET", "POST", Route = "{**method}")]
    [ApiExplorerSettings(IgnoreApi = true)]
    public IActionResult NotImplemented(string method)
        => SubsonicResult.Error(SubsonicErrorCodes.NotImplemented,
            $"'{method}' is not implemented by this server");

    // -- Helpers ---------------------------------------------------------------

    private static SubsonicResult NotFoundError()
        => SubsonicResult.Error(SubsonicErrorCodes.NotFound, "The requested data was not found");

    /// <summary>coverArt ids: a release, a track (via its release), or a raw artwork id.</summary>
    private async Task<ArtworkId?> ResolveArtworkId(string? id)
    {
        if (ArtworkId.TryParse(id, null, out var artworkId))
        {
            return artworkId;
        }

        if (ReleaseId.TryParse(id, null, out var releaseId))
        {
            return await _context.Releases.AsNoTracking()
                .Where(r => r.Id == releaseId)
                .Select(r => r.ArtworkId)
                .FirstOrDefaultAsync();
        }

        if (TrackId.TryParse(id, null, out var trackId))
        {
            return await _context.Tracks.AsNoTracking()
                .Where(t => t.Id == trackId)
                .Select(t => t.Disc.Release.ArtworkId)
                .FirstOrDefaultAsync();
        }

        return null;
    }

    private async Task<IActionResult> ServeAsset(AssetId assetId, string fallbackMime, string? downloadName = null)
    {
        var asset = await _context.Assets.AsNoTracking().FirstOrDefaultAsync(a => a.Id == assetId);
        if (asset == null)
        {
            return NotFoundError();
        }

        var path = _resolver.Resolve(asset.Root, asset.StorageKey);
        if (path == null || !System.IO.File.Exists(path))
        {
            return NotFoundError();
        }

        if (asset.ContentHash != null)
        {
            Response.Headers.ETag = $"\"{asset.ContentHash}\"";
        }

        var mime = asset.Mime ?? fallbackMime;
        return downloadName == null
            ? PhysicalFile(path, mime, enableRangeProcessing: true)
            : PhysicalFile(path, mime, downloadName, enableRangeProcessing: true);
    }

    private static string SafeFileName(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        return string.Concat(name.Select(c => invalid.Contains(c) ? '_' : c));
    }
}
