using Microsoft.EntityFrameworkCore;
using TlmcPlayerBackend.Ids;
using TlmcPlayerBackend.Models.MusicData;

namespace TlmcPlayerBackend.Search;

/// <summary>
/// The one projection from catalogue rows to search documents. Everything that
/// indexes — full rebuild, incremental reindex, per-track push after an internal
/// write — goes through <see cref="ProjectAsync"/>, so the document shape cannot
/// drift between callers (SCHEMA-V6.md section 7). The ETL does not build documents
/// itself; it calls the internal reindex endpoint and lands here too.
/// </summary>
public static class TrackSearchProjection
{
    /// <summary>EF-translatable shape; the locale split and formatting happen in
    /// memory in <see cref="ToDocument"/>.</summary>
    private class Row
    {
        public TrackId Id { get; set; }
        public LocalizedField Name { get; set; } = null!;
        public TimeSpan? Duration { get; set; }
        public bool HasMedia { get; set; }
        public bool HasLyrics { get; set; }

        public ReleaseId ReleaseId { get; set; }
        public LocalizedField ReleaseName { get; set; } = null!;
        public DateOnly? ReleaseDate { get; set; }
        public string? CatalogNumber { get; set; }

        public List<string> CircleNames { get; set; } = [];
        public List<string> Credits { get; set; } = [];
        public List<string> Tags { get; set; } = [];
        public List<LocalizedField> SongTitles { get; set; } = [];
        public List<LocalizedField> WorkFullNames { get; set; } = [];
        public List<LocalizedField> WorkShortNames { get; set; } = [];
    }

    public static async Task<List<TrackSearchDocument>> ProjectAsync(
        IQueryable<Track> tracks, CancellationToken ct)
    {
        var rows = await tracks
            .AsNoTracking()
            .Select(t => new Row
            {
                Id = t.Id,
                Name = t.Name,
                Duration = t.Duration,
                HasMedia = t.MediaKey != null,
                HasLyrics = t.LyricsId != null,
                ReleaseId = t.Disc.Release.Id,
                ReleaseName = t.Disc.Release.Name,
                ReleaseDate = t.Disc.Release.ReleaseDate,
                CatalogNumber = t.Disc.Release.CatalogNumber,
                CircleNames = t.Disc.Release.Circles
                    .OrderBy(rc => rc.Ordinal)
                    .Select(rc => rc.Circle.Name)
                    .ToList(),
                Credits = t.Credits
                    .OrderBy(c => c.Role).ThenBy(c => c.Ordinal)
                    .Select(c => c.CreditName)
                    .ToList(),
                Tags = t.Tags.Select(tt => tt.Tag.Name).ToList(),
                SongTitles = t.OriginalSongs
                    .Select(ts => ts.OriginalSong.Title)
                    .ToList(),
                WorkFullNames = t.OriginalSongs
                    .Select(ts => ts.OriginalSong.OriginalWork.FullName)
                    .ToList(),
                WorkShortNames = t.OriginalSongs
                    .Select(ts => ts.OriginalSong.OriginalWork.ShortName)
                    .ToList(),
            })
            .ToListAsync(ct);

        return rows.Select(ToDocument).ToList();
    }

    private static TrackSearchDocument ToDocument(Row row)
    {
        var workNames = row.WorkFullNames.Concat(row.WorkShortNames).ToList();

        return new TrackSearchDocument
        {
            Id = row.Id.ToString(),
            TitleDefault = row.Name.Default,
            TitleEn = row.Name.En,
            TitleZh = row.Name.Zh,
            TitleJp = row.Name.Jp,
            ReleaseId = row.ReleaseId.ToString(),
            ReleaseTitleDefault = row.ReleaseName.Default,
            ReleaseTitleEn = row.ReleaseName.En,
            ReleaseTitleZh = row.ReleaseName.Zh,
            ReleaseTitleJp = row.ReleaseName.Jp,
            ReleaseDate = row.ReleaseDate?.ToString("O"),
            CatalogNumber = row.CatalogNumber,
            CircleNames = row.CircleNames,
            Credits = row.Credits,
            Tags = row.Tags,
            OriginalSongTitlesDefault = Locale(row.SongTitles, f => f.Default),
            OriginalSongTitlesEn = Locale(row.SongTitles, f => f.En),
            OriginalSongTitlesZh = Locale(row.SongTitles, f => f.Zh),
            OriginalSongTitlesJp = Locale(row.SongTitles, f => f.Jp),
            OriginalWorkTitlesDefault = Locale(workNames, f => f.Default),
            OriginalWorkTitlesEn = Locale(workNames, f => f.En),
            OriginalWorkTitlesZh = Locale(workNames, f => f.Zh),
            OriginalWorkTitlesJp = Locale(workNames, f => f.Jp),
            DurationSeconds = row.Duration?.TotalSeconds,
            HasMedia = row.HasMedia,
            HasLyrics = row.HasLyrics,
        };
    }

    private static List<string> Locale(
        IEnumerable<LocalizedField> fields, Func<LocalizedField, string?> pick)
    {
        return fields
            .Select(pick)
            .Where(v => !string.IsNullOrWhiteSpace(v))
            .Select(v => v!)
            .Distinct()
            .ToList();
    }
}
