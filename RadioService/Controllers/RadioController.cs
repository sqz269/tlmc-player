using System.Diagnostics;
using ClientApi.MusicDataServiceClientApi.Api;
using ClientApi.MusicDataServiceClientApi.Model;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace RadioService.Controllers;

[ApiController]
[Route("api/radio")]
public class RadioController : Controller
{
    private readonly RadioSongProviderService _songProvider;

    public RadioController(RadioSongProviderService songProvider)
    {
        _songProvider = songProvider;
    }

    [HttpGet]
    public ActionResult<RadioSong> GetRadio()
    {
        // RadioSong is a struct so here result will be a copy instead of a reference
        var result = _songProvider.GetCurrent(out var error);
        if (result == null)
        {
            return Problem(
                title: "Failed to get current song",
                statusCode: StatusCodes.Status500InternalServerError,
                detail: error
            );
        }

        // since RadioSong is wrapped in Nullable<T>, the .Value will always return a copy
        // need a separate copy to modify it
        var r = result.Value;
        r.ElapsedTime = DateTime.UtcNow.Subtract(r.StartTime);
        return Ok(r);
    }
}