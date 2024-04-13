using AutoMapper;
using Microsoft.AspNetCore.JsonPatch;
using Microsoft.AspNetCore.JsonPatch.Operations;
using TlmcPlayerBackend.Dtos.MusicData.Track;
using TlmcPlayerBackend.Models.MusicData;

namespace TlmcPlayerBackend.Profiles.MusicData;

public class TrackProfile : Profile
{
    public TrackProfile()
    {
        CreateMap<JsonPatchDocument<TrackUpdateDtoForJsonPatch>, JsonPatchDocument<Track>>();
        //CreateMap<TrackUpdateDto, Track>()
        //    .ForMember(t => t.Original, t => t.Ignore());

        CreateMap<Operation<TrackUpdateDtoForJsonPatch>, Operation<Track>>();

        CreateMap<Track, TrackReadDto>();
        CreateMap<TrackWriteDto, Track>()
            .ForMember(t => t.TrackFile, t => t.Ignore());
    }
}
