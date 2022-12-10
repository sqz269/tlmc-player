using AutoMapper;
using MusicDataService.Dtos.Thumbnail;
using MusicDataService.Models;

namespace MusicDataService.Profiles;

public class ThumbnailProfile : Profile
{
    public ThumbnailProfile()
    {
        CreateMap<Thumbnail, ThumbnailReadDto>();
    }
}