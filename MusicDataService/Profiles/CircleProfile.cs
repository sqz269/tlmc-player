using AutoMapper;
using Microsoft.AspNetCore.JsonPatch;
using Microsoft.AspNetCore.JsonPatch.Operations;
using MusicDataService.Dtos.Circle;
using MusicDataService.Models;

namespace MusicDataService.Profiles;

public class CircleProfile : Profile
{
    public CircleProfile()
    {
        CreateMap(typeof(Operation<>), typeof(Operation<>));
        CreateMap(typeof(JsonPatchDocument<>), typeof(JsonPatchDocument<>));

        CreateMap<Circle, CircleReadDto>();
        CreateMap<CircleWriteDto, Circle>();
        CreateMap<JsonPatchDocument<CircleUpdateDto>, JsonPatchDocument<Circle>>();

        CreateMap<CircleWebsite, CircleWebsiteReadDto>();
        CreateMap<JsonPatchDocument<CircleWebsiteUpdateDto>, JsonPatchDocument<CircleWebsite>>();
    }
}