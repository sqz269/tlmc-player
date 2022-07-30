using AutoMapper;
using MusicDataService.Dtos;
using MusicDataService.Models;

namespace MusicDataService.Profiles;

public class TrackProfile : Profile
{
    public TrackProfile()
    {
        CreateMap<Track, TrackReadDto>();
        CreateMap<TrackWriteDto, Track>();
    }
}