using RadioService.SyncDataService;

namespace RadioService;

public class RadioEnqueuerService : IHostedService, IDisposable
{
    private readonly RadioSongProviderService _provider;
    private readonly MusicDataHttpApi _musicDataApi;
    private readonly ILogger _logger;
    private Timer? _timer;

    private const int QUEUE_SIZE = 10;

    public RadioEnqueuerService(RadioSongProviderService provider, ILogger<RadioEnqueuerService> logger, MusicDataHttpApi musicDataApi)
    {

        _provider = provider;
        _logger = logger;
        _musicDataApi = musicDataApi;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _timer = new Timer(EnqueuerWork, null, TimeSpan.Zero, TimeSpan.FromMinutes(1));
    }

    private async void EnqueuerWork(object? state)
    {
        var count = _provider.GetQueueCount(out var countError);

        switch (count)
        {
            case null:
                _logger.LogError("{} failed: {}", nameof(_provider.GetCurrent), countError);
                return;
            case >= QUEUE_SIZE:
                return;
        }

        var delta = QUEUE_SIZE - count.Value;

        // enqueue more songs to list
        var results = await _musicDataApi
            .GetAlbumApiInstance()
            .GetRandomSampleTrackWithHttpInfoAsync(limit: delta);

        if (results?.Data == null)
        {
            _logger.LogError("Call to MusicDataService for more songs failed: {}", results.ErrorText);
            return;
        }

        if (!_provider.AddToList(results.Data, out var addError))
        {
            _logger.LogError("{} failed: {}", nameof(_provider.AddToList), addError);
            return;
        }

        _logger.LogInformation("Enqueuer added: {} songs", delta);
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        _timer?.Change(Timeout.InfiniteTimeSpan, TimeSpan.Zero);
    }

    public void Dispose()
    {
        _timer?.Dispose();
    }
}