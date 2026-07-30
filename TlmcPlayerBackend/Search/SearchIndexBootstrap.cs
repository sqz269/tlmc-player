using Microsoft.Extensions.Options;

namespace TlmcPlayerBackend.Search;

/// <summary>
/// Applies the index settings contract at startup so a fresh deployment gets the
/// localizedAttributes configuration without a manual step — misconfigured CJK
/// segmentation is silent, which is exactly why this is not left to operators.
///
/// Deliberately a background service and not a startup gate: the API must come up
/// (and browse must work) with the engine down, so failures retry quietly and
/// eventually give up until the next boot or an explicit reindex call.
/// </summary>
public class SearchIndexBootstrap(
    IServiceScopeFactory scopeFactory,
    IOptions<SearchOptions> options,
    ILogger<SearchIndexBootstrap> logger) : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory = scopeFactory;
    private readonly SearchOptions _options = options.Value;
    private readonly ILogger<SearchIndexBootstrap> _logger = logger;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.Enabled)
        {
            _logger.LogInformation("Meilisearch is not configured; search endpoints will answer 503");
            return;
        }

        var delay = TimeSpan.FromSeconds(5);
        for (var attempt = 1; attempt <= 10; attempt++)
        {
            try
            {
                await using var scope = _scopeFactory.CreateAsyncScope();
                var search = scope.ServiceProvider.GetRequiredService<ISearchIndexService>();
                await search.EnsureIndexAsync(stoppingToken);
                _logger.LogInformation(
                    "Search index '{IndexUid}' settings applied", _options.IndexUid);
                return;
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (MeiliUnavailableException e)
            {
                _logger.LogWarning(
                    "Meilisearch unreachable during index bootstrap (attempt {Attempt}/10): {Message}",
                    attempt, e.Message);
            }
            catch (MeiliApiException e)
            {
                // A rejected settings payload will not fix itself by retrying.
                _logger.LogError(e, "Meilisearch rejected the index settings; search stays unconfigured");
                return;
            }

            await Task.Delay(delay, stoppingToken);
            delay = TimeSpan.FromSeconds(Math.Min(delay.TotalSeconds * 2, 60));
        }

        _logger.LogError(
            "Giving up on search index bootstrap; POST api/internal/search/reindex will retry it");
    }
}
