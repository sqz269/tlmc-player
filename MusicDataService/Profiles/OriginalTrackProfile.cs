using AutoMapper;
using MusicDataService.Dtos;
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