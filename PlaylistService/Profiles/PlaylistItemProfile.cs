using AutoMapper;
using PlaylistService.Dtos;
using PlaylistService.Model;

namespace PlaylistService.Profiles;

public class PlaylistItemProfile : Profile
{
    public PlaylistItemProfile()
    {
        CreateMap<PlaylistItem, PlaylistItemReadDto>();
        CreateMap<PlaylistItemReadDto, PlaylistItem>();
    }
}