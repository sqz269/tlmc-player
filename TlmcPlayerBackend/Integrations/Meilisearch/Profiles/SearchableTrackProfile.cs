using AutoMapper;
using TlmcPlayerBackend.Integrations.Meilisearch.Models;

namespace TlmcPlayerBackend.Integrations.Meilisearch.Profiles;

public class SearchableTrackProfile : Profile
{
    public SearchableTrackProfile()
    {
        CreateMap<TlmcPlayerBackend.Models.MusicData.Track,
                TlmcPlayerBackend.Integrations.Meilisearch.Models.SearchableTrack>()
            .ForMember(
                a => a.Id,
                o => o.MapFrom(a => a.Id.ToString()))
            .ForMember(
                a => a.Name,
                o => o.MapFrom(a => a.Name.Default))
            .ForMember(
                a => a.Index,
                o => o.MapFrom(a => a.Index))
            .ForMember(
                a => a.Disc,
                o => o.MapFrom(a => a.Disc))
            .ForMember(
                a => a.Duration,
                o => o.MapFrom(a => a.Duration.Value.Seconds))
            .ForMember(
                a => a.Genre,
                o => o.MapFrom(a => a.Genre))
            .ForMember(
                a => a.Staff,
                o => o.MapFrom(a => a.Staff))
            .ForMember(
                a => a.Arrangement,
                o => o.MapFrom(a => a.Arrangement))
            .ForMember(
                a => a.Vocalist,
                o => o.MapFrom(a => a.Vocalist))
            .ForMember(
                a => a.Lyricist,
                o => o.MapFrom(a => a.Lyricist))
            .ForMember(
                a => a.OriginalNonTouhou,
                o => o.MapFrom(a => a.OriginalNonTouhou))
            .ForMember(
                a => a.OriginalTracksPk,
                o => o.MapFrom(a => a.Original.Select(e => e.Id.ToString()).ToList()))
            .ForMember(
                a => a.OriginalTracksJp,
                o => o.MapFrom(a => a.Original.Select(e => e.Title.Jp).ToList()))
            .ForMember(
                a => a.OriginalTracksEn,
                o => o.MapFrom(a => a.Original.Select(e => e.Title.En).ToList()))
            .ForMember(
                a => a.OriginalTracksZh,
                o => o.MapFrom(a => a.Original.Select(e => e.Title.Zh).ToList()))
            .ForMember(
                a => a.OriginalAlbumsPk,
                o => o.MapFrom(a => a.Original.Select(e => e.Album.Id).ToList()))
            .ForMember(
                a => a.OriginalAlbumsJp,
                o => o.MapFrom(a => a.Original.Select(e => e.Album.FullName.Jp).ToList()))
            .ForMember(
                a => a.OriginalAlbumsEn,
                o => o.MapFrom(a => a.Original.Select(e => e.Album.FullName.En).ToList()))
            .ForMember(
                a => a.OriginalAlbumsZh,
                o => o.MapFrom(a => a.Original.Select(e => e.Album.FullName.Zh).ToList()))
            .ForMember(
                a => a.Album,
                o => o.MapFrom(a => a.Album.Id.ToString()));
    }
}