using AutoMapper;
using MusicDataService.Dtos;
using MusicDataService.Models;

namespace MusicDataService.Profiles;

public class AlbumProfile : Profile
{
    public AlbumProfile()
    {
        CreateMap<AlbumWriteDto, Album>();
        CreateMap<Album, AlbumReadDto>();
    }
}