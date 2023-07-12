using AutoMapper;
using MusicDataService.Dtos.Hls;
using MusicDataService.Models;

namespace MusicDataService.Profiles;

public class HlsAssetProfile : Profile
{
    public HlsAssetProfile()
    {
        CreateMap<HlsPlaylistWriteDto, HlsPlaylist>();
        CreateMap<HlsSegmentWriteDto, HlsSegment>();
    }
}