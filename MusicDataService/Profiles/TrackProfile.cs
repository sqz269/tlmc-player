using AutoMapper;
using MusicDataService.Data;
using MusicDataService.Data.Impl;
using MusicDataService.Dtos.Track;
using MusicDataService.Models;

namespace MusicDataService.Profiles;

public class TrackProfile : Profile
{
    public TrackProfile()
    {
        CreateMap<Track, TrackReadDto>();
        CreateMap<TrackWriteDto, Track>()
            .ForMember(t => t.TrackFile, t => t.Ignore());
    }
}
