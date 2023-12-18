using System.ComponentModel.DataAnnotations;
using Elasticsearch.Net;
using Microsoft.AspNetCore.Mvc;
using Nest;
using SearchService.Model;

namespace SearchService.Controllers;

[ApiController]
[Route("api/search")]
public class SearchController : Controller
{
    private readonly IElasticClient _elasticSearch;

    public SearchController(IElasticClient elasticSearch)
    {
        _elasticSearch = elasticSearch;
    }

    [HttpGet("albums", Name = nameof(SearchAlbums))]
    [ProducesResponseType(typeof(SearchResult<List<AlbumSearchResult>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<SearchResult<List<AlbumSearchResult>>>> SearchAlbums(string query, int start = 0, [Range(10, 50)] int limit=20)
    {
        var result = await _elasticSearch.SearchAsync<Album>(s => s
            .Index("albums")
            .Query(q => q
                .QueryString(qs => qs
                    .Query(query)
                )
            )
            .From(start)
            .Size(limit)
        );

        long nextStartIndex = start + result.Documents.Count == result.Total ? -1 : start + limit;
        var resp = new SearchResult<IList<AlbumSearchResult>>()
        {
            Query = query,
            Result = result.Hits.Select(hit => new AlbumSearchResult
            {
                Album = hit.Source,
                Score = hit.Score
            }).ToList(),
            Total = (int)result.Total,
            Size = result.Documents.Count,
            PrevStartIndex = start,
            NextStartIndex = nextStartIndex
        };

        return Ok(resp);
    }

    [HttpGet("tracks", Name = nameof(SearchTracks))]
    [ProducesResponseType(typeof(SearchResult<List<TrackSearchResult>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<SearchResult<List<TrackSearchResult>>>> SearchTracks(string query, int start = 0, [Range(10, 50)] int limit=20)
    {
        var result = await _elasticSearch.SearchAsync<Track>(s => s
            .Index("tracks")
            .Query(q => q
                .QueryString(qs => qs
                    .Query(query)
                )
            )
            .From(start)
            .Size(limit)
        );

        long nextStartIndex = start + result.Documents.Count == result.Total ? -1 : start + limit;
        var resp = new SearchResult<IList<TrackSearchResult>>()
        {
            Query = query,
            Result = result.Hits.Select(hit => new TrackSearchResult
            {
                Track = hit.Source,
                Score = hit.Score
            }).ToList(),
            Total = (int)result.Total,
            Size = result.Documents.Count,
            PrevStartIndex = start,
            NextStartIndex = nextStartIndex
        };

        return Ok(resp);
    }
}
