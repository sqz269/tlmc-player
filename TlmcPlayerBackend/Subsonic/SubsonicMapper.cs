using TlmcPlayerBackend.Data.Repos;
using TlmcPlayerBackend.Dtos.MusicData;
using TlmcPlayerBackend.Ids;

namespace TlmcPlayerBackend.Subsonic;

/// <summary>
/// v6 rows and DTOs → Subsonic wire shapes (Docs/SUBSONIC.md section 2).
/// Names serve the `default` localization only: per-request localization would
/// make ids appear to rename between clients and break their caches.
/// </summary>
public static class SubsonicMapper
{
    public static ArtistID3Dto ToArtist(ArtistRow row) => new()
    {
        Id = new CircleId(row.Id).ToString(),
        Name = row.Name,
        AlbumCount = row.AlbumCount,
    };

    public static AlbumID3Dto ToAlbum(AlbumRow row) => Fill(new AlbumID3Dto(), row);

    public static ArtistWithAlbumsDto ToArtistWithAlbums(ArtistRow row, List<AlbumRow> albums)
    {
        return new ArtistWithAlbumsDto
        {
            Id = new CircleId(row.Id).ToString(),
            Name = row.Name,
            AlbumCount = albums.Count,
            Album = albums.Select(ToAlbum).ToList(),
        };
    }

    public static AlbumWithSongsDto ToAlbumWithSongs(ReleaseReadDto release, DateTime createdAt, bool lossless)
    {
        var albumId = release.Id.ToString();
        var artist = release.Circles.FirstOrDefault();
        var coverArt = release.Artwork != null ? albumId : null;
        var tracks = release.Discs
            .SelectMany(d => d.Tracks.Select(t => (Disc: d, Track: t)))
            .ToList();

        var album = new AlbumWithSongsDto
        {
            Id = albumId,
            Name = release.Name.Default,
            Artist = artist?.Name,
            ArtistId = artist?.Id.ToString(),
            DisplayArtist = release.Circles.Count > 1
                ? string.Join(", ", release.Circles.Select(c => c.Name))
                : null,
            CoverArt = coverArt,
            SongCount = tracks.Count,
            Duration = (int)tracks
                .Aggregate(TimeSpan.Zero, (sum, t) => sum + (t.Track.Duration ?? TimeSpan.Zero))
                .TotalSeconds,
            Created = ToCreated(createdAt),
        };
        SetYear(album, release.ReleaseDate);

        album.Song = tracks.Select(pair =>
        {
            var child = new ChildDto
            {
                Id = pair.Track.Id.ToString(),
                Parent = albumId,
                AlbumId = albumId,
                Album = album.Name,
                Artist = album.Artist,
                ArtistId = album.ArtistId,
                Title = pair.Track.Name.Default,
                CoverArt = coverArt,
                DiscNumber = pair.Disc.DiscNumber,
                DiscNumberSpecified = true,
                Track = pair.Track.TrackNumber,
                TrackSpecified = true,
            };
            FillMedia(child, pair.Track.HasMedia, lossless);
            SetDuration(child, pair.Track.Duration);
            child.Path = SyntheticPath(album.Artist, album.Name, pair.Track.TrackNumber, child.Title, child.Suffix);
            return child;
        }).ToList();

        return album;
    }

    public static ChildDto ToSong(TrackWithContext track, bool lossless)
    {
        var albumId = track.Release.Id.ToString();
        var artist = track.Circles.FirstOrDefault();
        var child = new ChildDto
        {
            Id = track.Track.Id.ToString(),
            Parent = albumId,
            AlbumId = albumId,
            Album = track.Release.Name.Default,
            Artist = artist?.Name,
            ArtistId = artist?.Id.ToString(),
            Title = track.Track.Name.Default,
            CoverArt = track.ArtworkId != null ? albumId : null,
            Track = track.Track.TrackNumber,
            TrackSpecified = true,
        };
        FillMedia(child, track.Track.HasMedia, lossless);
        SetDuration(child, track.Track.Duration);
        child.Path = SyntheticPath(child.Artist, child.Album, track.Track.TrackNumber, child.Title, child.Suffix);
        return child;
    }

    public static ChildDto ToSong(TrackReadDto track, bool lossless)
    {
        var albumId = track.Release.Id.ToString();
        var artist = track.Circles.FirstOrDefault();
        var child = new ChildDto
        {
            Id = track.Id.ToString(),
            Parent = albumId,
            AlbumId = albumId,
            Album = track.Release.Name.Default,
            Artist = artist?.Name,
            ArtistId = artist?.Id.ToString(),
            Title = track.Name.Default,
            CoverArt = track.ArtworkId != null ? albumId : null,
            DiscNumber = track.DiscNumber,
            DiscNumberSpecified = true,
            Track = track.TrackNumber,
            TrackSpecified = true,
        };
        FillMedia(child, track.HasMedia, lossless);
        SetDuration(child, track.Duration);
        if (!lossless && track.HlsBitrates.Count > 0)
        {
            child.BitRate = track.HlsBitrates.Max();
            child.BitRateSpecified = true;
        }

        child.Path = SyntheticPath(child.Artist, child.Album, track.TrackNumber, child.Title, child.Suffix);
        return child;
    }

    /// <summary>search2's folder-flavored album entry.</summary>
    public static ChildDto ToAlbumChild(AlbumRow row)
    {
        var album = ToAlbum(row);
        return new ChildDto
        {
            Id = album.Id,
            Parent = album.ArtistId,
            IsDir = true,
            Title = album.Name,
            Album = album.Name,
            Artist = album.Artist,
            ArtistId = album.ArtistId,
            CoverArt = album.CoverArt,
            MediaType = "album",
        };
    }

    private static AlbumID3Dto Fill(AlbumID3Dto album, AlbumRow row)
    {
        var id = new ReleaseId(row.Id).ToString();
        album.Id = id;
        album.Name = row.Name;
        album.Artist = row.Artist;
        album.ArtistId = row.ArtistId is { } artistId ? new CircleId(artistId).ToString() : null;
        album.DisplayArtist = row.DisplayArtist != row.Artist ? row.DisplayArtist : null;
        album.CoverArt = row.ArtworkId != null ? id : null;
        album.SongCount = row.SongCount;
        album.Duration = row.DurationSec;
        album.Created = ToCreated(row.CreatedAt);
        SetYear(album, row.ReleaseDate);
        return album;
    }

    private static void FillMedia(ChildDto child, bool hasMedia, bool lossless)
    {
        if (!hasMedia && !lossless)
        {
            return;
        }

        child.Suffix = lossless ? "flac" : "m4a";
        child.ContentType = lossless ? "audio/flac" : "audio/mp4";
    }

    private static void SetDuration(ChildDto child, TimeSpan? duration)
    {
        if (duration is { } d)
        {
            child.Duration = (int)d.TotalSeconds;
            child.DurationSpecified = true;
        }
    }

    private static void SetYear(AlbumID3Dto album, DateOnly? releaseDate)
    {
        if (releaseDate is { } date)
        {
            album.Year = date.Year;
            album.YearSpecified = true;
        }
    }

    private static string ToCreated(DateTime value)
        => DateTime.SpecifyKind(value, DateTimeKind.Utc).ToString("yyyy-MM-dd'T'HH:mm:ss'Z'");

    /// <summary>
    /// Clients use `path` for display and download naming, never resolution —
    /// the collection's real layout stays private.
    /// </summary>
    private static string? SyntheticPath(string? artist, string? album, int track, string title, string? suffix)
    {
        if (suffix == null)
        {
            return null;
        }

        static string Clean(string? part) => (part ?? "Unknown").Replace('/', '-');
        return $"{Clean(artist)}/{Clean(album)}/{track:D2} {Clean(title)}.{suffix}";
    }
}
