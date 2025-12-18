using TlmcPlayerBackend.Models.Api;
using TlmcPlayerBackend.Models.MusicData;

namespace TlmcPlayerBackend.Utils;

public static class DiversitySampler
{
    public static IEnumerable<(Track track, double rank)> ApplyDiversitySampler(
        IEnumerable<(Track Track, double Distance)> candidates,
        int finalLimit,
        double diversityBias,
        TrackSimilarityRankingPenaltyAttribute penalties,
        double cosineThreshold=0.01)
    {
        var selected = new List<(Track track, double rank)>();
        // Clone the list so we can remove items as we pick them
        var remainingCandidates = candidates.ToList();

        // Remove candidates that exceed cosine distance threshold if specified
        // Prevent duplicates
        for (int i = remainingCandidates.Count - 1; i >= 0; i--)
        {
            if (penalties.HasFlag(TrackSimilarityRankingPenaltyAttribute.CosineDistThreshold) &&
                remainingCandidates[i].Distance < cosineThreshold)
            {
                remainingCandidates.RemoveAt(i);
            }
        }

        while (selected.Count < finalLimit && remainingCandidates.Count > 0)
        {
            double bestMmrScore = double.MinValue;
            var bestCandidateIndex = -1;

            for (int i = 0; i < remainingCandidates.Count; i++)
            {
                var candidate = remainingCandidates[i];

                // 1. Calculate Relevance (Vector Similarity)
                // pgvector returns Cosine Distance (0 to 2). 
                // We need Similarity (Higher is better).
                // Sim = 1 - Distance. (Assuming normalized vectors where dist is 0..2)
                double relevance = 1 - candidate.Distance;

                // 2. Calculate Redundancy (Similarity to already selected items)
                double maxRedundancy = 0.0;

                foreach (var selectedTrack in selected)
                {
                    double currentSim = CalculateAttributeSimilarity(candidate.Track, selectedTrack.track, penalties);
                    if (currentSim > maxRedundancy)
                    {
                        maxRedundancy = currentSim;
                    }
                }

                // 3. MMR Formula
                // Score = (Lambda * Relevance) - ((1 - Lambda) * Redundancy)
                double mmrScore = (diversityBias * relevance) - ((1 - diversityBias) * maxRedundancy);

                if (mmrScore > bestMmrScore)
                {
                    bestMmrScore = mmrScore;
                    bestCandidateIndex = i;
                }
            }

            // 4. Move best candidate to selected list
            if (bestCandidateIndex != -1)
            {
                selected.Add((remainingCandidates[bestCandidateIndex].Track, bestMmrScore));
                remainingCandidates.RemoveAt(bestCandidateIndex);
            }
            else
            {
                break;
            }
        }

        return selected;
    }

    private static double CalculateAttributeSimilarity(
        Track candidate,
        Track selected,
        TrackSimilarityRankingPenaltyAttribute penalties)
    {
        double similarityScore = 0.0;

        // If Artist penalty is on, checking for same artist
        if (penalties.HasFlag(TrackSimilarityRankingPenaltyAttribute.Artist))
        {
            var candidateArtists = candidate.Album.AlbumArtist.Select(a => a.Id).ToHashSet();
            var selectedArtists = selected.Album.AlbumArtist.Select(a => a.Id).ToHashSet();

            if (candidateArtists.Overlaps(selectedArtists))
            {
                Console.WriteLine($"Penalize {candidate.Name.Default} | Artist overlap");
                return 1.0;
            }
        }

        // If Title penalty is on, check for duplicate titles
        if (penalties.HasFlag(TrackSimilarityRankingPenaltyAttribute.Title))
        {
            if (LevenshteinDistance(
                    candidate.Name.Default,
                    selected.Name.Default) < 3) // Penalize if titles differ by less than 3 characters
            {
                Console.WriteLine($"Penalize {candidate.Name.Default} | Title overlap");
                return 1.0;
            }
        }

        // If Album penalty is on, checking for same album
        if (penalties.HasFlag(TrackSimilarityRankingPenaltyAttribute.Album))
        {
            if (candidate.Album.Id == selected.Album.Id)
            {
                Console.WriteLine(
                    $"Penalize {candidate.Name.Default} | Album overlap: {candidate.Album.Name.Default}");
                return 1.0;
            }
        }

        return similarityScore;
    }

    private static int LevenshteinDistance(string s, string t)
    {
        if (string.IsNullOrEmpty(s))
            return string.IsNullOrEmpty(t) ? 0 : t.Length;
        if (string.IsNullOrEmpty(t))
            return s.Length;
        var d = new int[s.Length + 1, t.Length + 1];
        for (int i = 0; i <= s.Length; i++)
            d[i, 0] = i;
        for (int j = 0; j <= t.Length; j++)
            d[0, j] = j;
        for (int i = 1; i <= s.Length; i++)
        {
            for (int j = 1; j <= t.Length; j++)
            {
                int cost = (s[i - 1] == t[j - 1]) ? 0 : 1;
                d[i, j] = Math.Min(
                    Math.Min(d[i - 1, j] + 1, d[i, j - 1] + 1),
                    d[i - 1, j - 1] + cost);
            }
        }

        return d[s.Length, t.Length];
    }
}