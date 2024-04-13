using AutoMapper;
using Microsoft.AspNetCore.JsonPatch;
using Microsoft.AspNetCore.JsonPatch.Operations;
using TlmcPlayerBackend.Dtos.MusicData.Album;
using TlmcPlayerBackend.Models.MusicData;

namespace TlmcPlayerBackend.Profiles.MusicData;

//public class AlbumUrlMapper : IMemberValueResolver<Album, AlbumReadDto>

public class AlbumProfile : Profile
{
    public AlbumProfile()
    {
        CreateMap<AlbumWriteDto, Album>()
            .ForMember(
                a => a.Image,
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