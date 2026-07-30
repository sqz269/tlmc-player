using Microsoft.EntityFrameworkCore;
using Pgvector.EntityFrameworkCore;
using TlmcPlayerBackend.Ids;
using TlmcPlayerBackend.Utils;

namespace TlmcPlayerBackend.Data.Repos;

public record SimilarCandidate(TrackWithContext Context, float Score);

public interface ISimilarityRepo
{
    /// <summary>Neighbours from the precomputed chamfer table, in rank order.</summary>
    Task<List<SimilarCandidate>> GetPrecomputed(TrackId trackId, int take);

    /// <summary>
    /// ANN fallback over the pooled vectors, for tracks the precompute has not seen.
    /// Throws EmbeddingNotFoundException when the track has no embedding either.
    /// </summary>
    Task<List<SimilarCandidate>> GetApproximate(TrackId trackId, int take);

    /// <summary>The embedding_config stamp — which basis produced these neighbours.</summary>
    Task<string?> GetModel();
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

    public Task<string?> GetModel()
    {
        return _context.EmbeddingConfigs
            .AsNoTracking()
            .Select(c => (string?)c.Model)
            .FirstOrDefaultAsync();
    }
}
