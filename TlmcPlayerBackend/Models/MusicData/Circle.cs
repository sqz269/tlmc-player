﻿using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TlmcPlayerBackend.Models.MusicData;

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
    [Key]
    public Guid Id { get; set; }

    public string Name { get; set; }
    public CircleStatus Status { get; set; }

    [Column(TypeName = "date")]
    public DateTime? Established { get; set; }

    public string? Country { get; set; }

    public List<string> Alias { get; set; } = new();

    public List<string> DataSource { get; set; } = new();

    // Stores the reference to the original TLMC directory (Raw name for the circle)
    public string? TlmcReference { get; set; }

    public List<CircleWebsite> Website { get; set; } = new();
    public List<Album>? Albums { get; set; } = new();
}