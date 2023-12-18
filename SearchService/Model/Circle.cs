using Nest;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MusicDataService.Models;

public enum CircleStatus {
    // Circle is active in touhou
    Active, 

    // Currently Inactive, but may be active in the future
    Inactive,

    // Circle is disbanded, will not be active in the future
    Disbanded,

    // Circle is still active, but not in touhou
    Transfer,

    // Unknown, but queried
    Unknown, 
    
    // Status not queried
    Unset
}

public class Circle
{
    [Keyword(Name = "Id")]
    public Guid Id { get; set; }

    [Text(Name = "Name")]
    public string Name { get; set; }

    [Text(Name = "Status")]
    public CircleStatus Status { get; set; }

    [Date(Name = "Established")]
    public DateTime? Established { get; set; }

    [Text(Name = "Country")]
    public string? Country { get; set; }

    [Text(Name = "Alias")]
    public List<string> Alias { get; set; } = new();
}