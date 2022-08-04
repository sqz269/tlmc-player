﻿using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MusicDataService.Models;

public class Track
{
    [Key]
    [Required]
    public Guid Id { get; set; }

    [Required]
    [Column(TypeName = "jsonb")]
    public LocalizedField Name { get; set; }

    [Required]
    public int? Index { get; set; }

    [Required]
    public int? Disc { get; set; }

    public List<string>? Genre { get; set; } = new();

    public List<string>? Arrangement { get; set; } = new();

    public List<string>? Vocalist { get; set; } = new();

    public List<string>? Lyricist { get; set; } = new();

    public bool? OriginalNonTouhou { get; set; }
    
    public Album Album { get; set; }

    public List<OriginalTrack>? Original { get; set; } = new();
}