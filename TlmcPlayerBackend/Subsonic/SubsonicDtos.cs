using System.Xml.Serialization;
using Newtonsoft.Json;

namespace TlmcPlayerBackend.Subsonic;

// The Subsonic wire shapes (Docs/SUBSONIC.md section 3). One set of DTOs serves
// both serializers: XmlSerializer reads the [Xml*] attributes, the facade's
// private Newtonsoft serializer camelCases the property names — which match the
// XML names by construction, so the two representations cannot drift.
//
// Optional numeric attributes use both omission conventions at once:
// `{Name}Specified` for XmlSerializer, `ShouldSerialize{Name}()` for Json.NET.

[XmlRoot("subsonic-response", Namespace = "http://subsonic.org/restapi")]
public class SubsonicEnvelope
{
    [XmlAttribute("status")] public string Status { get; set; } = "ok";
    [XmlAttribute("version")] public string Version { get; set; } = "1.16.1";
    [XmlAttribute("type")] public string Type { get; set; } = "tlmc";
    [XmlAttribute("serverVersion")] public string ServerVersion { get; set; } = "";
    [XmlAttribute("openSubsonic")] public bool OpenSubsonic { get; set; } = true;

    [XmlElement("error")] public SubsonicErrorDto? Error { get; set; }
    [XmlElement("license")] public LicenseDto? License { get; set; }
    [XmlElement("musicFolders")] public MusicFoldersDto? MusicFolders { get; set; }
    [XmlElement("artists")] public ArtistsDto? Artists { get; set; }
    [XmlElement("artist")] public ArtistWithAlbumsDto? Artist { get; set; }
    [XmlElement("album")] public AlbumWithSongsDto? Album { get; set; }
    [XmlElement("song")] public ChildDto? Song { get; set; }
    [XmlElement("albumList2")] public AlbumList2Dto? AlbumList2 { get; set; }
    [XmlElement("randomSongs")] public SongsDto? RandomSongs { get; set; }
    [XmlElement("searchResult3")] public SearchResult3Dto? SearchResult3 { get; set; }
    [XmlElement("searchResult2")] public SearchResult2Dto? SearchResult2 { get; set; }
    [XmlElement("similarSongs")] public SimilarSongsDto? SimilarSongs { get; set; }
    [XmlElement("similarSongs2")] public SimilarSongsDto? SimilarSongs2 { get; set; }
    [XmlElement("artistInfo2")] public ArtistInfo2Dto? ArtistInfo2 { get; set; }
    [XmlElement("openSubsonicExtensions")] public List<OpenSubsonicExtensionDto>? OpenSubsonicExtensions { get; set; }
}

public class SubsonicErrorDto
{
    [XmlAttribute("code")] public int Code { get; set; }
    [XmlAttribute("message")] public string Message { get; set; } = "";
}

public class LicenseDto
{
    [XmlAttribute("valid")] public bool Valid { get; set; } = true;
}

public class MusicFoldersDto
{
    [XmlElement("musicFolder")] public List<MusicFolderDto> MusicFolder { get; set; } = [];
}

public class MusicFolderDto
{
    [XmlAttribute("id")] public int Id { get; set; }
    [XmlAttribute("name")] public string Name { get; set; } = "";
}

public class ArtistsDto
{
    [XmlAttribute("ignoredArticles")] public string IgnoredArticles { get; set; } = "";
    [XmlElement("index")] public List<ArtistIndexDto> Index { get; set; } = [];
}

public class ArtistIndexDto
{
    [XmlAttribute("name")] public string Name { get; set; } = "";
    [XmlElement("artist")] public List<ArtistID3Dto> Artist { get; set; } = [];
}

public class ArtistID3Dto
{
    [XmlAttribute("id")] public string Id { get; set; } = "";
    [XmlAttribute("name")] public string Name { get; set; } = "";
    [XmlAttribute("albumCount")] public int AlbumCount { get; set; }
}

public class ArtistWithAlbumsDto : ArtistID3Dto
{
    [XmlElement("album")] public List<AlbumID3Dto> Album { get; set; } = [];
}

public class AlbumID3Dto
{
    [XmlAttribute("id")] public string Id { get; set; } = "";
    [XmlAttribute("name")] public string Name { get; set; } = "";
    [XmlAttribute("artist")] public string? Artist { get; set; }
    [XmlAttribute("artistId")] public string? ArtistId { get; set; }
    [XmlAttribute("displayArtist")] public string? DisplayArtist { get; set; }
    [XmlAttribute("coverArt")] public string? CoverArt { get; set; }
    [XmlAttribute("songCount")] public int SongCount { get; set; }
    [XmlAttribute("duration")] public int Duration { get; set; }
    [XmlAttribute("created")] public string Created { get; set; } = "";

    [XmlAttribute("year")] public int Year { get; set; }
    [XmlIgnore, JsonIgnore] public bool YearSpecified { get; set; }
    public bool ShouldSerializeYear() => YearSpecified;
}

public class AlbumWithSongsDto : AlbumID3Dto
{
    [XmlElement("song")] public List<ChildDto> Song { get; set; } = [];
}

/// <summary>Subsonic's do-everything song/directory-entry shape.</summary>
public class ChildDto
{
    [XmlAttribute("id")] public string Id { get; set; } = "";
    [XmlAttribute("parent")] public string? Parent { get; set; }
    [XmlAttribute("isDir")] public bool IsDir { get; set; }
    [XmlAttribute("title")] public string Title { get; set; } = "";
    [XmlAttribute("album")] public string? Album { get; set; }
    [XmlAttribute("artist")] public string? Artist { get; set; }
    [XmlAttribute("albumId")] public string? AlbumId { get; set; }
    [XmlAttribute("artistId")] public string? ArtistId { get; set; }
    [XmlAttribute("coverArt")] public string? CoverArt { get; set; }
    [XmlAttribute("suffix")] public string? Suffix { get; set; }
    [XmlAttribute("contentType")] public string? ContentType { get; set; }
    [XmlAttribute("path")] public string? Path { get; set; }
    [XmlAttribute("type"), JsonProperty("type")] public string MediaType { get; set; } = "music";

    [XmlAttribute("track")] public int Track { get; set; }
    [XmlIgnore, JsonIgnore] public bool TrackSpecified { get; set; }
    public bool ShouldSerializeTrack() => TrackSpecified;

    [XmlAttribute("discNumber")] public int DiscNumber { get; set; }
    [XmlIgnore, JsonIgnore] public bool DiscNumberSpecified { get; set; }
    public bool ShouldSerializeDiscNumber() => DiscNumberSpecified;

    [XmlAttribute("year")] public int Year { get; set; }
    [XmlIgnore, JsonIgnore] public bool YearSpecified { get; set; }
    public bool ShouldSerializeYear() => YearSpecified;

    [XmlAttribute("duration")] public int Duration { get; set; }
    [XmlIgnore, JsonIgnore] public bool DurationSpecified { get; set; }
    public bool ShouldSerializeDuration() => DurationSpecified;

    [XmlAttribute("bitRate")] public int BitRate { get; set; }
    [XmlIgnore, JsonIgnore] public bool BitRateSpecified { get; set; }
    public bool ShouldSerializeBitRate() => BitRateSpecified;
}

public class AlbumList2Dto
{
    [XmlElement("album")] public List<AlbumID3Dto> Album { get; set; } = [];
}

public class SongsDto
{
    [XmlElement("song")] public List<ChildDto> Song { get; set; } = [];
}

public class SimilarSongsDto
{
    [XmlElement("song")] public List<ChildDto> Song { get; set; } = [];
}

/// <summary>Only similarArtist is served; biography and the Last.fm fields have no source.</summary>
public class ArtistInfo2Dto
{
    [XmlElement("similarArtist")] public List<ArtistID3Dto> SimilarArtist { get; set; } = [];
}

public class SearchResult3Dto
{
    [XmlElement("artist")] public List<ArtistID3Dto> Artist { get; set; } = [];
    [XmlElement("album")] public List<AlbumID3Dto> Album { get; set; } = [];
    [XmlElement("song")] public List<ChildDto> Song { get; set; } = [];
}

/// <summary>Folder-flavored search shape: artists are bare id+name, albums are directory Children.</summary>
public class SearchResult2Dto
{
    [XmlElement("artist")] public List<ArtistDto> Artist { get; set; } = [];
    [XmlElement("album")] public List<ChildDto> Album { get; set; } = [];
    [XmlElement("song")] public List<ChildDto> Song { get; set; } = [];
}

public class ArtistDto
{
    [XmlAttribute("id")] public string Id { get; set; } = "";
    [XmlAttribute("name")] public string Name { get; set; } = "";
}

public class OpenSubsonicExtensionDto
{
    [XmlAttribute("name")] public string Name { get; set; } = "";
    [XmlElement("versions")] public List<int> Versions { get; set; } = [];
}
