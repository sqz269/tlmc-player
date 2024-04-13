using AutoMapper;
using Microsoft.AspNetCore.JsonPatch;
using Microsoft.AspNetCore.JsonPatch.Operations;
using TlmcPlayerBackend.Dtos.MusicData.Circle;
using TlmcPlayerBackend.Models.MusicData;

namespace TlmcPlayerBackend.Profiles.MusicData;

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