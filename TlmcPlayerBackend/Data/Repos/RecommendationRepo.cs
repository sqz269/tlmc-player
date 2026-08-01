using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using Pgvector;
using Pgvector.EntityFrameworkCore;
using TlmcPlayerBackend.Dtos.MusicData;
using TlmcPlayerBackend.Ids;
using TlmcPlayerBackend.Models.Playlist;
using TlmcPlayerBackend.Utils;

namespace TlmcPlayerBackend.Data.Repos;

public interface IRecommendationRepo
{
    Task<RecommendationHomeDto> GetHomeRows(UserId user);

    Task<RadioNextDto?> GetRadioNext(
        UserId user, TrackId? anchor, List<TrackId> completed,
        List<TrackId> skipped, List<TrackId> exclude, int count);
}

/// <summary>
/// Phase 1 of Docs/RECOMMENDER.md: content generates, behavior ranks. Play
/// history is condensed into completion- and recency-weighted taste centroids
/// in the ANN-indexed meanmax embedding space plus a per-family affinity
/// histogram; each home row draws candidates from embedding space, excludes
/// the recently played, passes the diversity reranker, and logs one
/// rec_impression per served id. Everything is derived on request — at
/// single-user scale the recompute is cheaper than a staleness bug.
/// </summary>
public class RecommendationRepo(
    AppDbContext context,
    ISimilarityRepo similarityRepo) : IRecommendationRepo
{
    private const int RowSize = 12;
    private const int CandidatePool = 36;
    private const int SignalWindow = 2000;
    private const double DecayHalfLifeDays = 60;
    private const int RecentlyPlayedDays = 30;
    private const int RediscoverDays = 90;

    private readonly AppDbContext _context = context;
    private readonly ISimilarityRepo _similarityRepo = similarityRepo;

    private sealed record TrackSignal(
        TrackId TrackId,
        double Weight,          // completion × decay, the taste signal
        double RawAffinity,     // completion only, for rediscover
        DateTime LastPlayedAt);

    public async Task<RecommendationHomeDto> GetHomeRows(UserId user)
    {
        var signals = await LoadSignals(user);
        if (signals.Count == 0)
        {
            return new RecommendationHomeDto();
        }

        var played = signals.Select(s => s.TrackId).ToHashSet();
        var recentlyPlayed = signals
            .Where(s => s.LastPlayedAt > DateTime.UtcNow.AddDays(-RecentlyPlayedDays))
            .Select(s => s.TrackId)
            .ToHashSet();

        var centroids = await ComputeCentroids(signals);

        var rows = new List<HomeRowDto?>
        {
            await BecauseYouPlayed(signals, recentlyPlayed),
            await Arrangements(signals, played),
            await Territory(centroids, played),
            await Rediscover(signals),
        };
        rows.AddRange(await FamilyMixes(signals, centroids, played));

        var dto = new RecommendationHomeDto
        {
            Rows = rows.Where(r => r is { TrackIds.Count: > 0 }).Select(r => r!).ToList(),
        };

        await LogImpressions(user, dto);
        return dto;
    }

    private async Task<List<TrackSignal>> LoadSignals(UserId user)
    {
        var events = await _context.PlayEvents.AsNoTracking()
            .Where(e => e.UserId == user)
            .OrderByDescending(e => e.PlayedAt)
            .Take(SignalWindow)
            .Select(e => new
            {
                e.TrackId,
                e.PlayedAt,
                e.MsPlayed,
                DurationSec = (double?)e.Track.Duration!.Value.TotalSeconds,
            })
            .ToListAsync();

        var now = DateTime.UtcNow;
        return events
            .GroupBy(e => e.TrackId)
            .Select(g =>
            {
                double weight = 0, raw = 0;
                foreach (var e in g)
                {
                    // Unknown listened time (third-party scrobbles report full
                    // duration, so only null is truly unknown) counts as a
                    // neutral half-listen.
                    var ratio = e.MsPlayed is { } ms && e.DurationSec is > 0
                        ? Math.Clamp(ms / (e.DurationSec.Value * 1000.0), 0, 1)
                        : 0.5;
                    var decay = Math.Pow(0.5, (now - e.PlayedAt).TotalDays / DecayHalfLifeDays);
                    weight += ratio * decay;
                    raw += ratio;
                }

                return new TrackSignal(g.Key, weight, raw, g.Max(e => e.PlayedAt));
            })
            .OrderByDescending(s => s.Weight)
            .ToList();
    }

    // -- Taste centroids -------------------------------------------------------

    /// <summary>
    /// Weighted k-means in meanmax space (the ANN-indexed column, so centroids
    /// can be query vectors directly). k grows stepwise with history instead of
    /// a silhouette sweep — with under a few dozen distinct tracks a second
    /// centroid is noise, and past that the exact k matters less than having
    /// more than one mode at all.
    /// </summary>
    private async Task<List<HalfVector>> ComputeCentroids(List<TrackSignal> signals)
    {
        var ids = signals.Select(s => s.TrackId).ToList();
        var rows = await _context.TrackEmbeddings.AsNoTracking()
            .Where(e => ids.Contains(e.TrackId))
            .Select(e => new { e.TrackId, e.EmbeddingMeanMax })
            .ToListAsync();
        if (rows.Count == 0)
        {
            return [];
        }

        var weightById = signals.ToDictionary(s => s.TrackId, s => s.Weight);
        var points = rows
            .Select(r => (Vec: ToUnitFloats(r.EmbeddingMeanMax), Weight: weightById[r.TrackId]))
            .ToList();

        var k = points.Count < 20 ? 1 : points.Count < 60 ? 2 : 3;
        var dims = points[0].Vec.Length;

        // k-means++ style init: heaviest point first, then farthest-from-chosen.
        var centroids = new List<float[]> { points.MaxBy(p => p.Weight).Vec };
        while (centroids.Count < k)
        {
            centroids.Add(points
                .MaxBy(p => centroids.Min(c => Distance(p.Vec, c))).Vec);
        }

        for (var iter = 0; iter < 12; iter++)
        {
            var sums = new double[k][];
            var mass = new double[k];
            for (var i = 0; i < k; i++)
            {
                sums[i] = new double[dims];
            }

            foreach (var (vec, weight) in points)
            {
                var best = 0;
                var bestDist = double.MaxValue;
                for (var i = 0; i < k; i++)
                {
                    var d = Distance(vec, centroids[i]);
                    if (d < bestDist)
                    {
                        bestDist = d;
                        best = i;
                    }
                }

                for (var d = 0; d < dims; d++)
                {
                    sums[best][d] += vec[d] * weight;
                }

                mass[best] += weight;
            }

            for (var i = 0; i < k; i++)
            {
                if (mass[i] <= 0)
                {
                    continue;
                }

                var next = new float[dims];
                for (var d = 0; d < dims; d++)
                {
                    next[d] = (float)(sums[i][d] / mass[i]);
                }

                centroids[i] = Normalize(next);
            }
        }

        return centroids.Select(c => new HalfVector(c.Select(v => (Half)v).ToArray())).ToList();
    }

    private static float[] ToUnitFloats(HalfVector v)
    {
        var span = v.Memory.Span;
        var floats = new float[span.Length];
        for (var i = 0; i < span.Length; i++)
        {
            floats[i] = (float)span[i];
        }

        return Normalize(floats);
    }

    private static float[] Normalize(float[] v)
    {
        var norm = Math.Sqrt(v.Sum(x => (double)x * x));
        if (norm <= 0)
        {
            return v;
        }

        for (var i = 0; i < v.Length; i++)
        {
            v[i] = (float)(v[i] / norm);
        }

        return v;
    }

    /// <summary>Squared euclidean over unit vectors — rank-equivalent to cosine distance.</summary>
    private static double Distance(float[] a, float[] b)
    {
        double sum = 0;
        for (var i = 0; i < a.Length; i++)
        {
            var d = a[i] - b[i];
            sum += d * d;
        }

        return sum;
    }

    // -- Rows ------------------------------------------------------------------

    private async Task<HomeRowDto?> BecauseYouPlayed(
        List<TrackSignal> signals, HashSet<TrackId> recentlyPlayed)
    {
        var anchor = signals.FirstOrDefault();
        if (anchor == null)
        {
            return null;
        }

        List<SimilarCandidate> candidates;
        try
        {
            candidates = await _similarityRepo.GetPrecomputed(anchor.TrackId, CandidatePool);
            if (candidates.Count == 0)
            {
                candidates = await _similarityRepo.GetApproximate(anchor.TrackId, CandidatePool);
            }
        }
        catch (EmbeddingNotFoundException)
        {
            return null;
        }

        var picked = DiversityReranker.Rerank(
                candidates
                    .Where(c => c.Context.Track.Id != anchor.TrackId
                                && !recentlyPlayed.Contains(c.Context.Track.Id))
                    .Select(c => new DiversityReranker.Candidate(
                        c.Context.Track.Id,
                        c.Score,
                        c.Context.Release.Id,
                        c.Context.Circles.Select(x => x.Id).ToList()))
                    .ToList(),
                RowSize)
            .Select(r => r.Candidate.TrackId)
            .ToList();

        // LocalizedField's jsonb keys don't project through LINQ; one raw scalar.
        var anchorName = (await _context.Database
            .SqlQueryRaw<string>(
                """SELECT t.name->>'default' AS "Value" FROM track t WHERE t.id = {0}""",
                anchor.TrackId.Value)
            .ToListAsync())
            .FirstOrDefault();

        return new HomeRowDto { Surface = "home.because", Title = anchorName, TrackIds = picked };
    }

    /// <summary>
    /// The corpus-unique row: unheard arrangements of the original songs behind
    /// the listener's favorites, nearest in sound to the loved arrangement.
    /// </summary>
    private async Task<HomeRowDto?> Arrangements(
        List<TrackSignal> signals, HashSet<TrackId> played)
    {
        // Contains over a value-converted id needs a materialized list to
        // translate (same constraint TrackRepo.Filter documents).
        var playedList = played.ToList();
        var loved = signals.Take(8).Select(s => s.TrackId).ToList();
        var lovedSongs = await _context.Set<Models.MusicData.TrackOriginalSong>().AsNoTracking()
            .Where(tos => loved.Contains(tos.TrackId))
            .Select(tos => new { tos.TrackId, tos.OriginalSongId })
            .ToListAsync();
        if (lovedSongs.Count == 0)
        {
            return null;
        }

        var perSong = new List<List<TrackId>>();
        foreach (var group in lovedSongs.GroupBy(x => x.OriginalSongId).Take(6))
        {
            var lovedTrack = group.First().TrackId;
            var lovedVec = await _context.TrackEmbeddings.AsNoTracking()
                .Where(e => e.TrackId == lovedTrack)
                .Select(e => e.EmbeddingMeanMax)
                .FirstOrDefaultAsync();
            if (lovedVec == null)
            {
                continue;
            }

            var songId = group.Key;
            perSong.Add(await _context.Set<Models.MusicData.TrackOriginalSong>().AsNoTracking()
                .Where(tos => tos.OriginalSongId == songId
                              && tos.Track.MediaKey != null
                              && !playedList.Contains(tos.TrackId))
                .OrderBy(tos => _context.TrackEmbeddings
                    .Where(e => e.TrackId == tos.TrackId)
                    .Select(e => e.EmbeddingMeanMax.CosineDistance(lovedVec))
                    .First())
                .Take(4)
                .Select(tos => tos.TrackId)
                .ToListAsync());
        }

        // Interleave across originals so one prolific tune cannot own the row.
        var picked = new List<TrackId>();
        for (var i = 0; picked.Count < RowSize; i++)
        {
            var advanced = false;
            foreach (var list in perSong.Where(list => i < list.Count))
            {
                if (!picked.Contains(list[i]))
                {
                    picked.Add(list[i]);
                    advanced = true;
                }

                if (picked.Count >= RowSize)
                {
                    break;
                }
            }

            if (!advanced)
            {
                break;
            }
        }

        return new HomeRowDto { Surface = "home.arrangements", TrackIds = picked };
    }

    /// <summary>
    /// Unheard tracks nearest each taste centroid. RECOMMENDER.md framed this as
    /// "new in your territory"; with the whole library imported in one batch,
    /// recency is meaningless and pure territory-kNN is the honest version.
    /// </summary>
    private async Task<HomeRowDto?> Territory(
        List<HalfVector> centroids, HashSet<TrackId> played)
    {
        var perCentroid = new List<List<DiversityReranker.Candidate>>();
        foreach (var centroid in centroids)
        {
            var rows = await _context.TrackEmbeddings.AsNoTracking()
                .OrderBy(e => e.EmbeddingMeanMax.CosineDistance(centroid))
                .Where(e => e.Track.MediaKey != null)
                .Take(CandidatePool)
                .Select(e => new
                {
                    e.TrackId,
                    Distance = e.EmbeddingMeanMax.CosineDistance(centroid),
                    ReleaseId = (ReleaseId?)e.Track.Disc.ReleaseId,
                    Circles = e.Track.Disc.Release.Circles.Select(rc => rc.CircleId).ToList(),
                })
                .ToListAsync();

            perCentroid.Add(rows
                .Where(r => !played.Contains(r.TrackId))
                .Select(r => new DiversityReranker.Candidate(
                    r.TrackId, 1f - (float)r.Distance, r.ReleaseId, r.Circles))
                .ToList());
        }

        var merged = perCentroid.SelectMany(x => x)
            .GroupBy(c => c.TrackId)
            .Select(g => g.OrderByDescending(c => c.Score).First())
            .ToList();

        return new HomeRowDto
        {
            Surface = "home.territory",
            TrackIds = DiversityReranker.Rerank(merged, RowSize)
                .Select(r => r.Candidate.TrackId).ToList(),
        };
    }

    private Task<HomeRowDto?> Rediscover(List<TrackSignal> signals)
    {
        var cutoff = DateTime.UtcNow.AddDays(-RediscoverDays);
        var picked = signals
            .Where(s => s.LastPlayedAt < cutoff)
            .OrderByDescending(s => s.RawAffinity)
            .Take(RowSize)
            .Select(s => s.TrackId)
            .ToList();

        return Task.FromResult<HomeRowDto?>(
            new HomeRowDto { Surface = "home.rediscover", TrackIds = picked });
    }

    private async Task<List<HomeRowDto?>> FamilyMixes(
        List<TrackSignal> signals, List<HalfVector> centroids, HashSet<TrackId> played)
    {
        if (centroids.Count == 0)
        {
            return [];
        }

        var ids = signals.Select(s => s.TrackId).ToList();
        var clusters = await _context.TrackMapPoints.AsNoTracking()
            .Where(p => ids.Contains(p.TrackId))
            .Select(p => new { p.TrackId, p.Cluster })
            .ToListAsync();
        var weightById = signals.ToDictionary(s => s.TrackId, s => s.Weight);

        var topFamilies = clusters
            .GroupBy(c => c.Cluster)
            .Select(g => new { Family = g.Key, Affinity = g.Sum(c => weightById[c.TrackId]) })
            .OrderByDescending(f => f.Affinity)
            .Take(2)
            .ToList();

        var rows = new List<HomeRowDto?>();
        foreach (var family in topFamilies)
        {
            var familyId = family.Family;
            var name = await _context.TrackMapClusters.AsNoTracking()
                .Where(c => c.Cluster == familyId)
                .Select(c => c.Name)
                .FirstOrDefaultAsync();

            // Within-family candidates near the closest centroid: the filtered
            // set is a few thousand rows, so the distance sort is a cheap scan.
            var centroid = centroids[0];
            var rowsForFamily = await _context.TrackMapPoints.AsNoTracking()
                .Where(p => p.Cluster == familyId && p.Track.MediaKey != null)
                .OrderBy(p => _context.TrackEmbeddings
                    .Where(e => e.TrackId == p.TrackId)
                    .Select(e => e.EmbeddingMeanMax.CosineDistance(centroid))
                    .First())
                .Take(CandidatePool)
                .Select(p => new
                {
                    p.TrackId,
                    ReleaseId = (ReleaseId?)p.Track.Disc.ReleaseId,
                    Circles = p.Track.Disc.Release.Circles.Select(rc => rc.CircleId).ToList(),
                })
                .ToListAsync();

            var candidates = rowsForFamily
                .Where(r => !played.Contains(r.TrackId))
                .Select((r, i) => new DiversityReranker.Candidate(
                    r.TrackId, 1f - i / (float)CandidatePool, r.ReleaseId, r.Circles))
                .ToList();

            rows.Add(new HomeRowDto
            {
                Surface = "home.family",
                Title = name,
                TrackIds = DiversityReranker.Rerank(candidates, RowSize)
                    .Select(r => r.Candidate.TrackId).ToList(),
            });
        }

        return rows;
    }

    // -- Adaptive radio (Docs/RECOMMENDER.md section 5) ------------------------

    private const float RocchioAlpha = 0.6f;
    private const float RocchioBeta = 0.3f;
    private const float RocchioGamma = 0.1f;

    /// <summary>
    /// Stateless Rocchio relevance feedback: the client sends the session so
    /// far, the query vector moves toward what was completed and away from
    /// what was skipped, and the same inputs always produce the same batch —
    /// which is also what makes the algorithm replayable in tests.
    /// </summary>
    public async Task<RadioNextDto?> GetRadioNext(
        UserId user, TrackId? anchor, List<TrackId> completed,
        List<TrackId> skipped, List<TrackId> exclude, int count)
    {
        var wanted = new List<TrackId>();
        if (anchor is { } a)
        {
            wanted.Add(a);
        }

        wanted.AddRange(completed);
        wanted.AddRange(skipped);
        wanted = wanted.Distinct().ToList();
        if (wanted.Count == 0)
        {
            return null;
        }

        var embeddings = await _context.TrackEmbeddings.AsNoTracking()
            .Where(e => wanted.Contains(e.TrackId))
            .Select(e => new { e.TrackId, e.EmbeddingMeanMax })
            .ToListAsync();
        var byId = embeddings.ToDictionary(e => e.TrackId, e => ToUnitFloats(e.EmbeddingMeanMax));

        var completedVecs = completed.Where(byId.ContainsKey).Select(id => byId[id]).ToList();
        var q0 = anchor is { } anchorId && byId.TryGetValue(anchorId, out var av)
            ? av
            : completedVecs.Count > 0 ? MeanOf(completedVecs) : null;
        if (q0 == null)
        {
            return null;
        }

        var skippedVecs = skipped.Where(byId.ContainsKey).Select(id => byId[id]).ToList();

        var q = new float[q0.Length];
        for (var i = 0; i < q.Length; i++)
        {
            q[i] = RocchioAlpha * q0[i];
        }

        AddScaled(q, completedVecs, RocchioBeta);
        AddScaled(q, skippedVecs, -RocchioGamma);
        var query = new HalfVector(Normalize(q).Select(v => (Half)v).ToArray());

        var excluded = exclude.Concat(wanted).ToHashSet();
        // Session tracks sit near the query by construction, so over-fetch by
        // the exclusion count to keep the post-filter pool full.
        var pool = Math.Clamp(count * 3, count, 100) + excluded.Count;

        var rows = await _context.TrackEmbeddings.AsNoTracking()
            .OrderBy(e => e.EmbeddingMeanMax.CosineDistance(query))
            .Where(e => e.Track.MediaKey != null)
            .Take(Math.Min(pool, 250))
            .Select(e => new
            {
                e.TrackId,
                Distance = e.EmbeddingMeanMax.CosineDistance(query),
                ReleaseId = (ReleaseId?)e.Track.Disc.ReleaseId,
                Circles = e.Track.Disc.Release.Circles.Select(rc => rc.CircleId).ToList(),
            })
            .ToListAsync();

        var picked = DiversityReranker.Rerank(
                rows.Where(r => !excluded.Contains(r.TrackId))
                    .Select(r => new DiversityReranker.Candidate(
                        r.TrackId, 1f - (float)r.Distance, r.ReleaseId, r.Circles))
                    .ToList(),
                count)
            .Select(r => r.Candidate.TrackId)
            .ToList();

        var dto = new RadioNextDto { TrackIds = picked };
        var context = JsonConvert.SerializeObject(new
        {
            anchor = anchor?.ToString(),
            completed = completed.Count,
            skipped = skipped.Count,
        });
        var now = DateTime.UtcNow;
        foreach (var trackId in picked)
        {
            _context.RecImpressions.Add(new RecImpression
            {
                UserId = user,
                TrackId = trackId,
                Surface = "radio",
                Context = context,
                ServedAt = now,
            });
        }

        await _context.SaveChangesAsync();
        return dto;
    }

    private static float[] MeanOf(List<float[]> vecs)
    {
        var mean = new float[vecs[0].Length];
        foreach (var vec in vecs)
        {
            for (var i = 0; i < mean.Length; i++)
            {
                mean[i] += vec[i];
            }
        }

        for (var i = 0; i < mean.Length; i++)
        {
            mean[i] /= vecs.Count;
        }

        return Normalize(mean);
    }

    private static void AddScaled(float[] target, List<float[]> vecs, float scale)
    {
        if (vecs.Count == 0)
        {
            return;
        }

        var mean = MeanOf(vecs);
        for (var i = 0; i < target.Length; i++)
        {
            target[i] += scale * mean[i];
        }
    }

    private async Task LogImpressions(UserId user, RecommendationHomeDto dto)
    {
        var now = DateTime.UtcNow;
        foreach (var row in dto.Rows)
        {
            var context = JsonConvert.SerializeObject(new { title = row.Title });
            foreach (var trackId in row.TrackIds)
            {
                _context.RecImpressions.Add(new RecImpression
                {
                    UserId = user,
                    TrackId = trackId,
                    Surface = row.Surface,
                    Context = context,
                    ServedAt = now,
                });
            }
        }

        await _context.SaveChangesAsync();
    }
}
