using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using MusicDataService.Models;

namespace MusicDataService.Dtos;

public class OriginalTrackReadDto
{
    public string Id { get; set; }

    public LocalizedField Title { get; set; }

    public OriginalAlbumReadDto Album { get; set; }
}