using Nest;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SearchService.Model;

public class OriginalAlbum
{
    [Keyword(Name = "Id")]
    public string Id { get; set; }

    [Keyword(Name = "Type")]
    public string Type { get; set; }

    [Object(Name = "FullName")]
    public LocalizedField FullName { get; set; }

    [Object(Name = "ShortName")]
    public LocalizedField ShortName { get; set; }

    [Keyword(Name = "ExternalReference")]
    public string ExternalReference { get; set; }
}