﻿using MusicDataService.Models;

namespace MusicDataService.Dtos;

public class AlbumReadDto
{
    public Guid Id { get; set; }

    // public List<Guid?>? LinkedAlbums { get; set; }

    public LocalizedField AlbumName { get; set; }

    public DateTime? ReleaseDate { get; set; }

    public string? ReleaseConvention { get; set; }

    public string? CatalogNumber { get; set; }

    public int? NumberOfDiscs { get; set; }

    public List<string>? Website { get; set; }

    public List<string>? AlbumArtist { get; set; }

    public List<string>? DataSource { get; set; }

    public List<TrackReadDto>? Tracks { get; set; }

    public AssetReadDto AlbumImage { get; set; }

    public List<AssetReadDto> OtherImages { get; set; }
}
