using TlmcPlayerBackend.Dtos.MusicData.Asset;
using TlmcPlayerBackend.Dtos.MusicData.Circle;
using TlmcPlayerBackend.Dtos.MusicData.Thumbnail;
using TlmcPlayerBackend.Dtos.MusicData.Track;
using TlmcPlayerBackend.Models.MusicData;

namespace TlmcPlayerBackend.Dtos.MusicData.Album;

public class AlbumReadDto
{
    public Guid Id { get; set; }

    public LocalizedField Name { get; set; }

    public DateTime? ReleaseDate { get; set; }

    public string? ReleaseConvention { get; set; }

    public string? CatalogNumber { get; set; }

    public int? NumberOfDiscs { get; set; }

    public int? DiscNumber { get; set; }

    public string? DiscName { get; set; }

    public List<string>? Website { get; set; }

    public List<CircleReadDto>? AlbumArtist { get; set; }

    public List<string>? DataSource { get; set; }

    public List<TrackReadDto>? Tracks { get; set; }

    public List<AlbumReadDto>? ChildAlbums { get; set; }

    public AlbumReadDto? ParentAlbum { get; set; }

    public ThumbnailReadDto? Thumbnail { get; set; }

    public List<AssetReadDto> OtherFiles { get; set; }
}
