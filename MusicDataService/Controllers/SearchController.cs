using AutoMapper;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MusicDataService.Data;
using MusicDataService.Data.Api;
using MusicDataService.Dtos;

namespace MusicDataService.Controllers;

class AlbumSearchQuery
{
    public string? Title { get; set; }
    public DateTime ReleaseDateBegin { get; set; }
    public DateTime ReleaseDateEnd { get; set; }
    public string? ReleaseConvention { get; set; }
    public string? CatalogNumber { get; set; }
    public string? AlbumArtist { get; set; }
}

class TrackSearchQuery
{
    public string? Title { get; set; }
    public List<string>? OriginalTracks { get; set; } = new();
    public List<string>? Staff { get; set; } = new();
}

[ApiController]
[Route("api/search")]
public class SearchController
{
    private readonly AppDbContext _context;
    private readonly IMapper _mapper;
    public SearchController(AppDbContext context, IMapper mapper)
    {
        _context = context;
        _mapper = mapper;
    }

    //[HttpGet("album")]
    //public async Task<IEnumerable<AlbumReadDto>> SearchAlbums([FromQuery] AlbumSearchQuery query)
    //{
        
    //}

    //[HttpGet("track")]
    //public async Task<IEnumerable<TrackReadDto>> SearchTracks([FromQuery] TrackSearchQuery query)
    //{

    //}
}