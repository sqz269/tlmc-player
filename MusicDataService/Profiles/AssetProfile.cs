using AutoMapper;
using MusicDataService.Dtos;
using MusicDataService.Models;

namespace MusicDataService.Profiles;

public class AssetProfile : Profile
{
    public AssetProfile()
    {
        CreateMap<Asset, AssetReadDto>()
            .ForMember(a => a.Url, t => t.Ignore());
    }
}