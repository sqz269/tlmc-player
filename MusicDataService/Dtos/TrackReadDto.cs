using MusicDataService.Models;

namespace MusicDataService.Dtos;

public class TrackReadDto
{
    public Guid Id { get; set; }

    public List<Guid>? Linked { get; set; }

    public LocalizedField Name { get; set; }

    public int? Index { get; set; }

    public int? Disc { get; set; }

    public List<string>? Genre { get; set; }

    public List<string>? Arrangement { get; set; }

    public List<string>? Vocalist { get; set; }

    public List<string>? Lyricist { get; set; }

    public List<OriginalTrackReadDto>? Original { get; set; }

    public bool? OriginalNonTouhou { get; set; }
}