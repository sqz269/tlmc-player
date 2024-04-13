using AutoMapper;
using TlmcPlayerBackend.Dtos.MusicData.OriginalAlbum;
using TlmcPlayerBackend.Models.MusicData;

namespace TlmcPlayerBackend.Profiles.MusicData;

public class OriginalAlbumProfile : Profile
{
    public OriginalAlbumProfile()
    {
        CreateMap<OriginalAlbum, OriginalAlbumReadDto>();
        CreateMap<OriginalAlbumWriteDto, OriginalAlbum>();
    }
}