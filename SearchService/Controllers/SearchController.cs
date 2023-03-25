using System.ComponentModel.DataAnnotations;
using Elasticsearch.Net;
using Microsoft.AspNetCore.Mvc;
using Nest;

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

    [HttpGet("albums")]
    public async Task<ActionResult<object>> SearchAlbums(string query, int start = 0, [Range(10, 50)] int limit=20)
    {
        var result = await _elasticSearch.SearchAsync<object>(s => s
            .Index("albums")
            .Query(q => q
                .QueryString(qs => qs
                    .Query(query)
                )
            )
            .From(start)
            .Size(limit)
        );

        var resp = new
        {
            Query = query,
            Result = result.Documents,
            Total = result.Total
        };

        return Ok(resp);
    }

    [HttpGet("tracks")]
    public async Task<object> SearchTracks(string query, int start = 0, [Range(10, 50)] int limit=20)
    {
        var result = await _elasticSearch.SearchAsync<object>(s => s
            .Index("tracks")
            .Query(q => q
                .QueryString(qs => qs
                    .Query(query)
                )
            )
            .From(start)
            .Size(limit)
        );

        var resp = new
        {
            Query = query,
            Result = result.Documents,
            Total = result.Total
        };

        return Ok(resp);
    }
}
