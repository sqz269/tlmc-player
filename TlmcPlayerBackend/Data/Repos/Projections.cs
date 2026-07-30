using TlmcPlayerBackend.Dtos.MusicData;
using TlmcPlayerBackend.Ids;
using TlmcPlayerBackend.Models.MusicData;

namespace TlmcPlayerBackend.Data.Repos;

/// <summary>A track plus the context most list surfaces need (release, artwork, circles).</summary>
public class TrackWithContext
{
    public TrackListItemDto Track { get; set; } = null!;
    public ReleaseSlimDto Release { get; set; } = null!;
    public ArtworkId? ArtworkId { get; set; }
    public List<CircleSlimDto> Circles { get; set; } = [];
}

/// <summary>Row shape for keyset id queries (see Cursor): the id plus its sort value.</summary>
public class KeysetRow
{
    public Guid Id { get; set; }
    public string? Sort { get; set; }
}

public static class Projections
{
    public static IQueryable<TrackListItemDto> ToTrackListItems(this IQueryable<Track> tracks)
    {
        return tracks.Select(t => new TrackListItemDto
        {
            Id = t.Id,
            TrackNumber = t.TrackNumber,
            Name = t.Name,
            Duration = t.Duration,
            HasMedia = t.MediaKey != null,
            HasLyrics = t.LyricsId != null,
        });
    }

    public static IQueryable<TrackWithContext> ToTrackWithContext(this IQueryable<Track> tracks)
    {
        return tracks.Select(t => new TrackWithContext
        {
            Track = new TrackListItemDto
            {
                Id = t.Id,
                TrackNumber = t.TrackNumber,
                Name = t.Name,
                Duration = t.Duration,
                HasMedia = t.MediaKey != null,
                HasLyrics = t.LyricsId != null,
            },
            Release = new ReleaseSlimDto
            {
                Id = t.Disc.Release.Id,
                Name = t.Disc.Release.Name,
            },
            ArtworkId = t.Disc.Release.ArtworkId,
            Circles = t.Disc.Release.Circles
                .OrderBy(rc => rc.Ordinal)
                .Select(rc => new CircleSlimDto { Id = rc.CircleId, Name = rc.Circle.Name })
                .ToList(),
        });
    }

    /// <summary>Reorders hydrated rows to match the keyset id order.</summary>
    public static List<T> InIdOrder<T>(this IEnumerable<T> rows, IReadOnlyList<Guid> ids, Func<T, Guid> id)
    {
        var index = new Dictionary<Guid, int>(ids.Count);
        for (var i = 0; i < ids.Count; i++)
        {
            index[ids[i]] = i;
        }

        return rows.OrderBy(r => index[id(r)]).ToList();
    }
}
