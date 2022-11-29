using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using MusicDataService.Dtos.OriginalAlbum;
using MusicDataService.Models;

namespace MusicDataService.Dtos.OriginalTrack;

public class OriginalTrackReadDto
{
    public string Id { get; set; }

    public LocalizedField Title { get; set; }

    public OriginalAlbumReadDto Album { get; set; }
}