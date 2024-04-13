using AutoMapper;
using TlmcPlayerBackend.Dtos.MusicData.Hls;
using TlmcPlayerBackend.Models.MusicData;

namespace TlmcPlayerBackend.Profiles.MusicData;

public class HlsAssetProfile : Profile
{
    public HlsAssetProfile()
    {
        CreateMap<HlsPlaylistWriteDto, HlsPlaylist>();
        CreateMap<HlsSegmentWriteDto, HlsSegment>();
    }
}