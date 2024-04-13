using TlmcPlayerBackend.Models.MusicData;

namespace TlmcPlayerBackend.Dtos.MusicData.Track;

public class TrackUpdateDtoForJsonPatch
{
    public LocalizedField Name { get; set; }

    public List<string>? Genre { get; set; }

    public List<string>? Staff { get; set; }

    public List<string>? Arrangement { get; set; }

    public List<string>? Vocalist { get; set; }

    public List<string>? Lyricist { get; set; }

    public bool? OriginalNonTouhou { get; set; }
}