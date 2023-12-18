using Nest;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SearchService.Model;

public class Track
{
    [Keyword(Name = "Id")]
    public Guid Id { get; set; }

    [Object(Name = "Name")]
    public LocalizedField Name { get; set; } = new LocalizedField();

    [Number(Name = "Index")]
    public int Index { get; set; }

    [Number(Name = "Disc")]
    public int Disc { get; set; }

    //[Date(Name = "Duration", Format = "HH:mm:ss")]
    [Text(Name = "Duration")]
    public string Duration { get; set; }

    [Keyword(Name = "Genre")]
    public List<string> Genre { get; set; } = new List<string>();

    [Keyword(Name = "Staff")]
    public List<string> Staff { get; set; } = new List<string>();

    [Keyword(Name = "Arrangement")]
    public List<string> Arrangement { get; set; } = new List<string>();

    [Keyword(Name = "Vocalist")]
    public List<string> Vocalist { get; set; } = new List<string>();

    [Keyword(Name = "lyricist")]
    public List<string> Lyricist { get; set; } = new List<string>();

    [Nested(Name = "OriginalTracks")]
    public List<OriginalTrack> OriginalTracks { get; set; } = new List<OriginalTrack>();
}