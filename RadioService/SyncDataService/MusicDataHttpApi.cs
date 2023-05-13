using ClientApi.MusicDataServiceClientApi.Api;
using ClientApi.MusicDataServiceClientApi.Client;

namespace RadioService.SyncDataService;

public class MusicDataHttpApi
{
    private readonly ILogger<MusicDataHttpApi> _logger;
    private readonly string _musicDataBaseUrl;
    public MusicDataHttpApi(IConfiguration configuration, ILogger<MusicDataHttpApi> logger)
    {
        _logger = logger;
        var baseUrl = configuration.GetSection("ClientApiBaseUrl")["MusicDataService"];
        _musicDataBaseUrl = baseUrl;
        logger.LogInformation("Using base URL: {}", _musicDataBaseUrl);
    }

    public AlbumApi GetAlbumApiInstance(Configuration? config=null)
    {
        if (config == null)
        {
            return new AlbumApi(_musicDataBaseUrl);
        }

        config.BasePath = _musicDataBaseUrl;
        return new AlbumApi(config);
    }
}