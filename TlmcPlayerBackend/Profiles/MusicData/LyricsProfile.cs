using AutoMapper;
using Microsoft.AspNetCore.JsonPatch.Operations;
using Microsoft.AspNetCore.JsonPatch;
using TlmcPlayerBackend.Dtos.MusicData.Circle;
using TlmcPlayerBackend.Dtos.MusicData.Lyrics;
using TlmcPlayerBackend.Models.MusicData;

namespace TlmcPlayerBackend.Profiles.MusicData;

public class LyricsProfile : Profile
{
    public LyricsProfile()
    {
        CreateMap<Ruby, RubyDto>();
        CreateMap<LyricsText, LyricsTextDto>();
        CreateMap<LyricsLine, LyricsLineDto>();
        CreateMap<LyricsVariant, LyricsVariantDto>();
        CreateMap<Lyrics, LyricsReadDto>();
    }
}