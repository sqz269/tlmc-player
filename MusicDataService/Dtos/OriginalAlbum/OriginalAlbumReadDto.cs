using System.ComponentModel.DataAnnotations.Schema;
using MusicDataService.Models;

namespace MusicDataService.Dtos.OriginalAlbum;

public class OriginalAlbumReadDto
{
    public string Id { get; set; }

    public string Type { get; set; }

    public LocalizedField FullName { get; set; }

    public LocalizedField ShortName { get; set; }
}