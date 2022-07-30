using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace MusicDataService.Models;

public class Album
{
    [BsonId]
    [BsonRepresentation(BsonType.String)]
    public Guid Id { get; set; }

    [BsonRepresentation(BsonType.String)]
    public List<Guid>? LinkedAlbums { get; set; } = new();

    public LocalizedField AlbumName { get; set; } = new();

    public DateTime? ReleaseDate { get; set; }

    public string? ReleaseConvention { get; set; }

    public string? CatalogNumber { get; set; }

    public int? NumberOfDiscs { get; set; }

    public string? Website { get; set; }

    public List<string>? AlbumArtist { get; set; } = new();

    public List<string>? Genre { get; set; } = new();

    public List<string>? DataSource { get; set; } = new();

    public List<Track>? Tracks { get; set; } = new();
}