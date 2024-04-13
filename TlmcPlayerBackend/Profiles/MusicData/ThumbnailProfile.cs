using AutoMapper;
using TlmcPlayerBackend.Dtos.MusicData.Thumbnail;
using TlmcPlayerBackend.Models.MusicData;

namespace TlmcPlayerBackend.Profiles.MusicData;

public class ThumbnailProfile : Profile
{
    public ThumbnailProfile()
    {
        CreateMap<Thumbnail, ThumbnailReadDto>();
    }
}