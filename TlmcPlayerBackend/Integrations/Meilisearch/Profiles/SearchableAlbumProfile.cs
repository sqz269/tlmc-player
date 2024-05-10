using AutoMapper;

namespace TlmcPlayerBackend.Integrations.Meilisearch.Profiles;

public class SearchableAlbumProfile : Profile
{
    public SearchableAlbumProfile()
    {
        CreateMap<TlmcPlayerBackend.Models.MusicData.Album, TlmcPlayerBackend.Integrations.Meilisearch.Models.SearchableAlbum>()
            .ForMember(
                a => a.Id,
                o => o.MapFrom(a => a.Id.ToString()))
            .ForMember(
                a => a.Name,
                o => o.MapFrom(a => a.Name.Default))
            .ForMember(
                a => a.ReleaseDate,
                o => o.MapFrom(a => a.ReleaseDate.Value.Second))
            .ForMember(
                a => a.ReleaseConvention,
                o => o.MapFrom(a => a.ReleaseConvention))
            .ForMember(
                a => a.CatalogNumber,
                o => o.MapFrom(a => a.CatalogNumber))
            .ForMember(
                a => a.AlbumArtists,
                o => o.MapFrom(a => a.AlbumArtist.Select(e => e.Name).ToList()))
            .ForMember(
                a => a.TrackNames,
                o => o.MapFrom(a => a.Tracks.Select(e => e.Name.Default).ToList()));
    }
}