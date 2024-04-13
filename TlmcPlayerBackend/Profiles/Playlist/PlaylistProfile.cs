  using AutoMapper;
using TlmcPlayerBackend.Dtos.Playlist;

namespace TlmcPlayerBackend.Profiles.Playlist;

public class PlaylistProfile : Profile
{
    public PlaylistProfile()
    {
        CreateMap<Models.Playlist.Playlist, PlaylistReadDto>();
    }
}