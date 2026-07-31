using TlmcPlayerBackend.Ids;
using TlmcPlayerBackend.Models.MusicData;

namespace TlmcPlayerBackend.Dtos.MusicData;

public class TrackMapWorkDto
{
    public OriginalWorkId Id { get; set; }
    public LocalizedField ShortName { get; set; } = null!;
}

public class TrackMapCircleDto
{
    public CircleId Id { get; set; }
    public string Name { get; set; } = null!;

    /// <summary>Points on the map credited to this circle.</summary>
    public int Count { get; set; }
}

/// <summary>
/// The whole library as one payload of parallel arrays — entry i of every array
/// describes the same point. Arrays instead of an object per track because the map
/// is ~164k points: repeated keys would triple the payload for nothing.
/// </summary>
public class TrackMapResponseDto
{
    public int Count { get; set; }

    /// <summary>From embedding_config — the basis the layout was computed from.</summary>
    public string? Model { get; set; }

    /// <summary>Legend for the work array, in stable id order.</summary>
    public List<TrackMapWorkDto> Works { get; set; } = [];

    /// <summary>Legend for the circle array, largest circle first — an index
    /// threshold is all a client needs for top-N coloring.</summary>
    public List<TrackMapCircleDto> Circles { get; set; } = [];

    public List<TrackId> Ids { get; set; } = [];
    public List<float> X { get; set; } = [];
    public List<float> Y { get; set; } = [];

    /// <summary>Acoustic-family label, 0 = largest; -1 means unlabeled.</summary>
    public List<short> Cluster { get; set; } = [];

    /// <summary>0 when the release is undated.</summary>
    public List<short> Year { get; set; } = [];

    /// <summary>Index into works; -1 when the track arranges no known work.</summary>
    public List<short> Work { get; set; } = [];

    /// <summary>Index into circles; -1 when uncredited.</summary>
    public List<short> Circle { get; set; } = [];
}
