using Nest;
using System.ComponentModel.DataAnnotations;

namespace SearchService.Model;

public class LocalizedField
{
    [Text(Name = "Default")]
    public string Default { get; set; }

    [Text(Name = "En")]
    public string En { get; set; }

    [Text(Name = "Zh")]
    public string Zh { get; set; }

    [Text(Name = "Jp")]
    public string Jp { get; set; }
}