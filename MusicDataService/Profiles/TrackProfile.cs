using AutoMapper;
using Microsoft.AspNetCore.JsonPatch.Operations;
using Microsoft.AspNetCore.JsonPatch;
using MusicDataService.Data;
using MusicDataService.Data.Impl;
using MusicDataService.Dtos.Album;
using MusicDataService.Dtos.Track;
using MusicDataService.Models;

namespace MusicDataService.Profiles;

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
