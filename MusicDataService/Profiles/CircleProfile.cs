using AutoMapper;
using MusicDataService.Dtos.Circle;
using MusicDataService.Models;

namespace MusicDataService.Profiles;

public class CircleProfile : Profile
{
    public CircleProfile()
    {
        CreateMap<Circle, CircleReadDto>();
        CreateMap<CircleWriteDto, Circle>();
    }
}