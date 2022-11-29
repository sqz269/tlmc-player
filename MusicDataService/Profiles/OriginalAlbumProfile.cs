using AutoMapper;
using MusicDataService.Dtos.OriginalAlbum;
using MusicDataService.Models;

namespace MusicDataService.Profiles;

public class OriginalAlbumProfile : Profile
{
    public OriginalAlbumProfile()
    {
        CreateMap<OriginalAlbum, OriginalAlbumReadDto>();
        CreateMap<OriginalAlbumWriteDto, OriginalAlbum>();
    }
}