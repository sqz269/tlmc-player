using AutoMapper;
using TlmcPlayerBackend.Dtos.Playlist;
using TlmcPlayerBackend.Models.Playlist;

namespace TlmcPlayerBackend.Profiles.Playlist;

public class PlaylistItemProfile : Profile
{
    public PlaylistItemProfile()
    {
        CreateMap<PlaylistItem, PlaylistItemReadDto>();
        CreateMap<PlaylistItemReadDto, PlaylistItem>();
    }
}