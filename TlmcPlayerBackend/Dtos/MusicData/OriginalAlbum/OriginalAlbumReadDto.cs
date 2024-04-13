using TlmcPlayerBackend.Models.MusicData;

namespace TlmcPlayerBackend.Dtos.MusicData.OriginalAlbum;

public class OriginalAlbumReadDto
{
    public string Id { get; set; }

    public string Type { get; set; }

    public LocalizedField FullName { get; set; }

    public LocalizedField ShortName { get; set; }
}