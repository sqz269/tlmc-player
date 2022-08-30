using AutoMapper;
using MusicDataService.Dtos;
using MusicDataService.Models;

namespace MusicDataService.Profiles;

public class AlbumProfile : Profile
{
    public AlbumProfile()
    {
        CreateMap<AlbumWriteDto, Album>()
            .ForMember(
                a => a.AlbumImage,
                o => o.Ignore())
            .ForMember(
                a => a.OtherImages,
                o => o.Ignore());
        CreateMap<Album, AlbumReadDto>();
    }
}