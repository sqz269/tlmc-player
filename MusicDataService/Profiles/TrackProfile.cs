using AutoMapper;
using MusicDataService.Data;
using MusicDataService.Dtos;
using MusicDataService.Models;

namespace MusicDataService.Profiles;

public class TrackProfile : Profile
{
    public TrackProfile()
    {
        CreateMap<Track, TrackReadDto>();
        CreateMap<TrackWriteDto, Track>()
            .ForMember(t => t.Original, t => t.Ignore());
    }
}
