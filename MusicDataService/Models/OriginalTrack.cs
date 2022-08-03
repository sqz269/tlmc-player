﻿using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MusicDataService.Models;

public class OriginalTrack
{
    [Key]
    public string Id { get; set; }

    public Album Album { get; set; }

    [Column(TypeName = "jsonb")]
    public LocalizedField Title { get; set; }
}