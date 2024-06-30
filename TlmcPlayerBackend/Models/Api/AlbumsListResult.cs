using TlmcPlayerBackend.Dtos.MusicData.Album;

namespace TlmcPlayerBackend.Models.Api;

public class AlbumsListResult
{
    public List<AlbumReadDto> Albums { get; set; }
    public int Count { get; set; }
    public long Total { get; set; }
}