using AutoMapper;
using TlmcPlayerBackend.Dtos.MusicData.OriginalTrack;
using TlmcPlayerBackend.Models.MusicData;

namespace TlmcPlayerBackend.Profiles.MusicData;

public class OriginalTrackProfile : Profile
{
    public OriginalTrackProfile()
    {
        CreateMap<OriginalTrack, OriginalTrackReadDto>();
        CreateMap<OriginalTrackWriteDto, OriginalTrack>();
    }
}