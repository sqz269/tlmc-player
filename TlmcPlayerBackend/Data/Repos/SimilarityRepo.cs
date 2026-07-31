using Microsoft.EntityFrameworkCore;
using Pgvector.EntityFrameworkCore;
using TlmcPlayerBackend.Dtos.MusicData;
using TlmcPlayerBackend.Ids;
using TlmcPlayerBackend.Models.MusicData;
using TlmcPlayerBackend.Utils;

namespace TlmcPlayerBackend.Data.Repos;

public record SimilarCandidate(TrackWithContext Context, float Score);

/// <summary>Which precomputed ordering a group-similarity read follows.</summary>
public enum GroupSimilarityFlavor
{
    /// <summary>Duplicate-suppressed chamfer — "sounds like", shared recordings demoted.</summary>
    Style,

    /// <summary>Kernel mean embedding cosine — mass-weighted, display-friendly scale.</summary>
    Kde,
}

public static class SimilarityFlavor
{
    /// <summary>Query-string form: 'style' (default) or 'kde'. 'raw' is deliberately
    /// not an option — shared recordings are the other-releases feature.</summary>
    public static bool TryParse(string? value, out GroupSimilarityFlavor flavor)
    {
        switch (value?.ToLowerInvariant())
        {
            case null or "" or "style":
                flavor = GroupSimilarityFlavor.Style;
                return true;
            case "kde":
                flavor = GroupSimilarityFlavor.Kde;
                return true;
            default:
                flavor = GroupSimilarityFlavor.Style;
                return false;
        }
    }
}

public record SimilarReleaseCandidate(
    ReleaseSlimDto Release, ArtworkId? ArtworkId, List<CircleSlimDto> Circles,
    float ScoreStyle, float ScoreRaw, float ScoreKde);

public record SimilarCircleCandidate(
    CircleSlimDto Circle, float ScoreStyle, float ScoreRaw, float ScoreKde);

public interface ISimilarityRepo
{
    /// <summary>Neighbours from the precomputed chamfer table, in rank order.</summary>
    Task<List<SimilarCandidate>> GetPrecomputed(TrackId trackId, int take);

    /// <summary>
    /// ANN fallback over the pooled vectors, for tracks the precompute has not seen.
    /// Throws EmbeddingNotFoundException when the track has no embedding either.
    /// </summary>
    Task<List<SimilarCandidate>> GetApproximate(TrackId trackId, int take);

    /// <summary>
    /// Precomputed release neighbours in the flavor's rank order. No ANN fallback:
    /// releases outside the precompute legitimately have nothing to say.
    /// </summary>
    Task<List<SimilarReleaseCandidate>> GetSimilarReleases(
        ReleaseId releaseId, GroupSimilarityFlavor flavor, int take);

    /// <summary>
    /// Releases sharing recordings with this one (raw ordering, score floor at
    /// minRawScore) — re-releases, compilations, duplicate rips. A versions
    /// feature, deliberately separate from similarity.
    /// </summary>
    Task<List<SimilarReleaseCandidate>> GetOtherReleases(
        ReleaseId releaseId, float minRawScore, int take);

    /// <summary>Precomputed circle neighbours in the flavor's rank order.</summary>
    Task<List<SimilarCircleCandidate>> GetSimilarCircles(
        CircleId circleId, GroupSimilarityFlavor flavor, int take);

    /// <summary>The embedding_config stamp — which basis produced these neighbours.</summary>
    Task<string?> GetModel();

    /// <summary>The precomputed 2D embedding map; empty until the ETL load runs.</summary>
    Task<TrackMapResponseDto> GetTrackMap();
}

public class SimilarityRepo(AppDbContext context) : ISimilarityRepo
{
    // Matches the fix from 8b97201: the hnsw.ef_search default of 40 silently caps
    // any larger over-fetch, so the scope must be raised alongside the limit.
    private const int DefaultEfSearch = 40;
    private const int MaxEfSearch = 1000;

    private readonly AppDbContext _context = context;

    public async Task<List<SimilarCandidate>> GetPrecomputed(TrackId trackId, int take)
    {
        var rows = await _context.SimilarTracks
            .AsNoTracking()
            .Where(s => s.AnchorTrackId == trackId)
            .OrderBy(s => s.Rank)
            .Take(take)
            .Select(s => new
            {
                s.Score,
                Context = new TrackWithContext
                {
                    Track = new Dtos.MusicData.TrackListItemDto
                    {
                        Id = s.NeighborTrack.Id,
                        TrackNumber = s.NeighborTrack.TrackNumber,
                        Name = s.NeighborTrack.Name,
                        Duration = s.NeighborTrack.Duration,
                        HasMedia = s.NeighborTrack.MediaKey != null,
                        HasLyrics = s.NeighborTrack.LyricsId != null,
                    },
                    Release = new Dtos.MusicData.ReleaseSlimDto
                    {
                        Id = s.NeighborTrack.Disc.Release.Id,
                        Name = s.NeighborTrack.Disc.Release.Name,
                    },
                    ArtworkId = s.NeighborTrack.Disc.Release.ArtworkId,
                    Circles = s.NeighborTrack.Disc.Release.Circles
                        .OrderBy(rc => rc.Ordinal)
                        .Select(rc => new Dtos.MusicData.CircleSlimDto { Id = rc.CircleId, Name = rc.Circle.Name })
                        .ToList(),
                },
            })
            .ToListAsync();

        return rows.Select(r => new SimilarCandidate(r.Context, r.Score)).ToList();
    }

    public async Task<List<SimilarCandidate>> GetApproximate(TrackId trackId, int take)
    {
        var anchor = await _context.TrackEmbeddings
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.TrackId == trackId);

        if (anchor == null)
        {
            throw new EmbeddingNotFoundException(trackId.Value);
        }

        // SET LOCAL scopes the raised ef_search to this transaction. The +1 /
        // self-filter mirrors the v5 fix: the nearest neighbour of a track is
        // itself.
        await using var transaction = await _context.Database.BeginTransactionAsync();

        var efSearch = Math.Clamp(take + 1, DefaultEfSearch, MaxEfSearch);
        await _context.Database.ExecuteSqlRawAsync($"SET LOCAL hnsw.ef_search = {efSearch}");

        var query = anchor.EmbeddingMeanMax;
        var neighbours = await _context.TrackEmbeddings
            .AsNoTracking()
            .OrderBy(e => e.EmbeddingMeanMax.CosineDistance(query))
            .Select(e => new
            {
                e.TrackId,
                Distance = e.EmbeddingMeanMax.CosineDistance(query),
            })
            .Take(take + 1)
            .ToListAsync();

        await transaction.CommitAsync();

        var picked = neighbours
            .Where(n => n.TrackId != trackId)
            .Take(take)
            .ToList();

        if (picked.Count == 0)
        {
            return [];
        }

        var ids = picked.Select(p => p.TrackId.Value).ToList();
        var typedIds = picked.Select(p => p.TrackId).ToList();
        var contexts = await _context.Tracks
            .AsNoTracking()
            .Where(t => typedIds.Contains(t.Id))
            .ToTrackWithContext()
            .ToListAsync();

        var byId = contexts.ToDictionary(c => c.Track.Id);
        return picked
            .Where(p => byId.ContainsKey(p.TrackId))
            .Select(p => new SimilarCandidate(byId[p.TrackId], (float)(1.0 - p.Distance)))
            .ToList();
    }

    public async Task<List<SimilarReleaseCandidate>> GetSimilarReleases(
        ReleaseId releaseId, GroupSimilarityFlavor flavor, int take)
    {
        var rows = _context.SimilarReleases
            .AsNoTracking()
            .Where(s => s.AnchorReleaseId == releaseId);
        rows = flavor == GroupSimilarityFlavor.Kde
            ? rows.Where(s => s.RankKde != null).OrderBy(s => s.RankKde)
            : rows.Where(s => s.RankStyle != null).OrderBy(s => s.RankStyle);
        return await ProjectReleases(rows.Take(take));
    }

    public async Task<List<SimilarReleaseCandidate>> GetOtherReleases(
        ReleaseId releaseId, float minRawScore, int take)
    {
        var rows = _context.SimilarReleases
            .AsNoTracking()
            .Where(s => s.AnchorReleaseId == releaseId
                        && s.RankRaw != null && s.ScoreRaw >= minRawScore)
            .OrderBy(s => s.RankRaw);
        return await ProjectReleases(rows.Take(take));
    }

    public async Task<List<SimilarCircleCandidate>> GetSimilarCircles(
        CircleId circleId, GroupSimilarityFlavor flavor, int take)
    {
        var rows = _context.SimilarCircles
            .AsNoTracking()
            .Where(s => s.AnchorCircleId == circleId);
        rows = flavor == GroupSimilarityFlavor.Kde
            ? rows.Where(s => s.RankKde != null).OrderBy(s => s.RankKde)
            : rows.Where(s => s.RankStyle != null).OrderBy(s => s.RankStyle);
        return await rows
            .Take(take)
            .Select(s => new SimilarCircleCandidate(
                new CircleSlimDto { Id = s.NeighborCircle.Id, Name = s.NeighborCircle.Name },
                s.ScoreStyle, s.ScoreRaw, s.ScoreKde))
            .ToListAsync();
    }

    private static Task<List<SimilarReleaseCandidate>> ProjectReleases(
        IQueryable<SimilarRelease> rows)
    {
        return rows
            .Select(s => new SimilarReleaseCandidate(
                new ReleaseSlimDto { Id = s.NeighborRelease.Id, Name = s.NeighborRelease.Name },
                s.NeighborRelease.ArtworkId,
                s.NeighborRelease.Circles
                    .OrderBy(rc => rc.Ordinal)
                    .Select(rc => new CircleSlimDto { Id = rc.CircleId, Name = rc.Circle.Name })
                    .ToList(),
                s.ScoreStyle, s.ScoreRaw, s.ScoreKde))
            .ToListAsync();
    }

    public Task<string?> GetModel()
    {
        return _context.EmbeddingConfigs
            .AsNoTracking()
            .Select(c => (string?)c.Model)
            .FirstOrDefaultAsync();
    }

    public async Task<TrackMapResponseDto> GetTrackMap()
    {
        var points = await _context.TrackMapPoints
            .AsNoTracking()
            .OrderBy(p => p.TrackId)
            .Select(p => new { p.TrackId, p.X, p.Y, p.Cluster, p.Year, p.WorkId, p.CircleId })
            .ToListAsync();

        var workIds = points
            .Where(p => p.WorkId != null)
            .Select(p => p.WorkId!.Value)
            .Distinct()
            .ToList();
        var works = await _context.OriginalWorks
            .AsNoTracking()
            .Where(w => workIds.Contains(w.Id))
            .OrderBy(w => w.Id)
            .Select(w => new TrackMapWorkDto { Id = w.Id, ShortName = w.ShortName })
            .ToListAsync();
        var workIndex = new Dictionary<OriginalWorkId, short>();
        for (short i = 0; i < works.Count; i++)
        {
            workIndex[works[i].Id] = i;
        }

        var circleCounts = points
            .Where(p => p.CircleId != null)
            .GroupBy(p => p.CircleId!.Value)
            .ToDictionary(g => g.Key, g => g.Count());
        var circleIds = circleCounts.Keys.ToList();
        var circleNames = await _context.Circles
            .AsNoTracking()
            .Where(c => circleIds.Contains(c.Id))
            .Select(c => new { c.Id, c.Name })
            .ToDictionaryAsync(c => c.Id, c => c.Name);
        var circles = circleCounts
            .Select(kv => new TrackMapCircleDto
            {
                Id = kv.Key,
                Name = circleNames.GetValueOrDefault(kv.Key) ?? string.Empty,
                Count = kv.Value,
            })
            .OrderByDescending(c => c.Count)
            .ThenBy(c => c.Name)
            .ToList();
        var circleIndex = new Dictionary<CircleId, short>();
        for (short i = 0; i < circles.Count; i++)
        {
            circleIndex[circles[i].Id] = i;
        }

        var clusterNames = await _context.TrackMapClusters
            .AsNoTracking()
            .OrderBy(c => c.Cluster)
            .Select(c => new TrackMapClusterDto { Id = c.Cluster, Name = c.Name })
            .ToListAsync();

        var map = new TrackMapResponseDto
        {
            Count = points.Count,
            Model = await GetModel(),
            Works = works,
            Circles = circles,
            Clusters = clusterNames,
        };
        foreach (var p in points)
        {
            map.Ids.Add(p.TrackId);
            map.X.Add(p.X);
            map.Y.Add(p.Y);
            map.Cluster.Add(p.Cluster);
            map.Year.Add(p.Year ?? 0);
            map.Work.Add(p.WorkId is { } workId ? workIndex[workId] : (short)-1);
            map.Circle.Add(p.CircleId is { } circleId ? circleIndex[circleId] : (short)-1);
        }

        return map;
    }
}
