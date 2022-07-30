using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace MusicDataService.Models;

public class Track
{
    [BsonId]
    [BsonRepresentation(BsonType.String)]
    public Guid Id { get; set; }

    [BsonRepresentation(BsonType.String)]
    public List<Guid>? Linked { get; set; } = new();

    public LocalizedField Name { get; set; }

    public int? Index { get; set; }

    public int? Disc { get; set; }

    public List<string>? Genre { get; set; } = new();

    public List<string>? Arrangement { get; set; } = new();

    public List<string>? Vocalist { get; set; } = new();

    public List<string>? Lyricist { get; set; } = new();

    public List<OriginalTrack>? Original { get; set; } = new();

    public bool? OriginalNonTouhou { get; set; }
}