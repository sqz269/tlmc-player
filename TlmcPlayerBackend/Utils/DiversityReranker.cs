using TlmcPlayerBackend.Ids;

namespace TlmcPlayerBackend.Utils;

/// <summary>
/// Greedy re-rank of similarity candidates so one release or circle cannot flood
/// the neighbour list. Chamfer scores compress into roughly 0.986..0.994, so raw
/// scores are min-max normalized within the candidate set first and the penalties
/// operate on that 0..1 relevance scale.
/// </summary>
public static class DiversityReranker
{
    public record Candidate(
        TrackId TrackId,
        float Score,
        ReleaseId? ReleaseId,
        IReadOnlyList<CircleId> CircleIds);

    private const float ReleasePenalty = 0.18f;
    private const float CirclePenalty = 0.10f;

    public static List<(Candidate Candidate, float Relevance)> Rerank(
        IReadOnlyList<Candidate> candidates, int take)
    {
        if (candidates.Count == 0)
        {
            return [];
        }

        var min = candidates.Min(c => c.Score);
        var max = candidates.Max(c => c.Score);
        var span = max - min;
        float Normalize(float s) => span <= 0 ? 1f : (s - min) / span;

        var remaining = candidates.ToList();
        var picked = new List<(Candidate, float)>();
        var releaseSeen = new Dictionary<ReleaseId, int>();
        var circleSeen = new Dictionary<CircleId, int>();

        while (picked.Count < take && remaining.Count > 0)
        {
            Candidate? best = null;
            var bestAdjusted = float.MinValue;
            var bestRelevance = 0f;

            foreach (var c in remaining)
            {
                var relevance = Normalize(c.Score);
                var adjusted = relevance;
                if (c.ReleaseId is { } rel && releaseSeen.TryGetValue(rel, out var relCount))
                {
                    adjusted -= ReleasePenalty * relCount;
                }

                foreach (var circle in c.CircleIds)
                {
                    if (circleSeen.TryGetValue(circle, out var cirCount))
                    {
                        adjusted -= CirclePenalty * cirCount;
                    }
                }

                if (adjusted > bestAdjusted)
                {
                    bestAdjusted = adjusted;
                    best = c;
                    bestRelevance = relevance;
                }
            }

            if (best is null)
            {
                break;
            }

            picked.Add((best, bestRelevance));
            remaining.Remove(best);
            if (best.ReleaseId is { } pickedRelease)
            {
                releaseSeen[pickedRelease] = releaseSeen.GetValueOrDefault(pickedRelease) + 1;
            }

            foreach (var circle in best.CircleIds)
            {
                circleSeen[circle] = circleSeen.GetValueOrDefault(circle) + 1;
            }
        }

        return picked;
    }
}
