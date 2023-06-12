using AutoMapper;
using Microsoft.AspNetCore.JsonPatch;
using Microsoft.AspNetCore.JsonPatch.Operations;
using MusicDataService.Dtos.Album;
using MusicDataService.Models;

namespace MusicDataService.Profiles;

//public class AlbumUrlMapper : IMemberValueResolver<Album, AlbumReadDto>

public class AlbumProfile : Profile
{
    public AlbumProfile()
    {
        CreateMap<AlbumWriteDto, Album>()
            .ForMember(
                a => a.AlbumImage,
                o => o.Ignore())
            .ForMember(
                a => a.OtherFiles,
                o => o.Ignore())
            .ForMember(
                a => a.AlbumArtist,
                o => o.Ignore());

        CreateMap<Album, AlbumReadDto>()
            .ForMember(dest => dest.ChildAlbums, 
                opt => opt.MapFrom(src => src.ChildAlbums))
            .ForMember(dest => dest.ParentAlbum, 
                opt => opt.MapFrom(src => src.ParentAlbum))
            .MaxDepth(2);

        CreateMap<JsonPatchDocument<AlbumUpdateDto>, JsonPatchDocument<Album>>();
        CreateMap<Operation<AlbumUpdateDto>, Operation<Album>>();
    }
}