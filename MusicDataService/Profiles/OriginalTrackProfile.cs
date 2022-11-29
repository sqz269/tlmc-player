using AutoMapper;
using MusicDataService.Dtos.OriginalTrack;
using MusicDataService.Models;

namespace MusicDataService.Profiles;

public class OriginalTrackProfile : Profile
{
    public OriginalTrackProfile()
    {
        CreateMap<OriginalTrack, OriginalTrackReadDto>();
        CreateMap<OriginalTrackWriteDto, OriginalTrack>();
    }
}