using Nest;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SearchService.Model;

public class OriginalTrack
{
    [Keyword(Name = "Id")]
    public string Id { get; set; }

    [Object(Name = "Title")]
    public LocalizedField Title { get; set; }

    [Nested(Name = "OriginalAlbums")]
    public OriginalAlbum OriginalAlbums { get; set; }
}