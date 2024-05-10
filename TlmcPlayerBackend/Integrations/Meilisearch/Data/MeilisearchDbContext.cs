using Meilisearch;
using TlmcPlayerBackend.Integrations.Meilisearch.Models;
using Index = Meilisearch.Index;

namespace TlmcPlayerBackend.Integrations.MeiliSearch.Data;

public class MeilisearchDbContext
{
    public MeilisearchClient Client { get; set; }

    public Index SearchableAlbums { get; set; }

    public Index SearchableTracks { get; set; }

    public MeilisearchDbContext(
        string host,
        string masterKey
    )
    {
        Client = new MeilisearchClient(host, masterKey);
    }

    public async Task<TaskResource> AwaitTask(int taskUid)
    {
        while (true)
        {
            var taskStatus = await Client.GetTaskAsync(taskUid);

            if (taskStatus.Status is not (TaskInfoStatus.Enqueued or TaskInfoStatus.Processing))
            {
                return taskStatus;
            }
        }
    }

    private async Task<Index> CreateOrGetIndex(string indexName)
    {
        var allIndices = await Client.GetAllIndexesAsync();
        var allIndicesName = allIndices.Results.Select(r => r.Uid);

        if (!allIndicesName.Contains(indexName))
        {
            var task = await Client.CreateIndexAsync(indexName);

            var taskResult = await AwaitTask(task.TaskUid);
            if (taskResult.Status != TaskInfoStatus.Succeeded)
            {
                throw new Exception($"Failed to create Meilisearch Index {indexName}. Task returned status: {task.Status} != success");
            }
        }

        return Client.Index(indexName);
    }

    public async Task CreateModelsAndInstantiateDatabase()
    {
        SearchableAlbums = await CreateOrGetIndex(nameof(SearchableAlbums));
        SearchableTracks = await CreateOrGetIndex(nameof(SearchableTracks));
    }
}