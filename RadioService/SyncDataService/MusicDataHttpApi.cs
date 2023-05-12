using ClientApi.MusicDataServiceClientApi.Api;
using ClientApi.MusicDataServiceClientApi.Client;

namespace RadioService.SyncDataService;

public class MusicDataHttpApi
{
    private readonly string _musicDataBaseUrl;
    public MusicDataHttpApi(IConfiguration configuration)
    {
        var baseUrl = configuration.GetSection("ClientApiBaseUrl")["MusicDataService"];
        _musicDataBaseUrl = baseUrl;
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