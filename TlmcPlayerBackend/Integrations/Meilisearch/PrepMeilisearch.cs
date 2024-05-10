using AutoMapper;
using Meilisearch;
using Microsoft.EntityFrameworkCore;
using TlmcPlayerBackend.Data;
using TlmcPlayerBackend.Integrations.MeiliSearch.Data;
using TlmcPlayerBackend.Integrations.Meilisearch.Models;
using static System.Net.Mime.MediaTypeNames;
using AutoMapper.Internal;
using System.Reflection;
using TlmcPlayerBackend.Models.MusicData;

namespace TlmcPlayerBackend.Integrations.Meilisearch;

public class PrepMeilisearch
{
    private MeilisearchDbContext meilisearchDbContext;
    private AppDbContext appDbContext;
    private IMapper mapper;
    private bool isProduction;

    public PrepMeilisearch(IApplicationBuilder applicationBuilder, IWebHostEnvironment hostEnvironment)
    {
        var serviceScope = applicationBuilder.ApplicationServices.CreateAsyncScope();

        meilisearchDbContext = serviceScope.ServiceProvider.GetService<MeilisearchDbContext>();
        appDbContext = serviceScope.ServiceProvider.GetService<AppDbContext>();
        mapper = serviceScope.ServiceProvider.GetService<IMapper>();

        isProduction = hostEnvironment.IsProduction();

        if (meilisearchDbContext == null)
        {
            throw new ArgumentException(
                "Failed to find MeilisearchDbContext. Was the class registered for DI?");
        }

        if (appDbContext == null)
        {
            throw new ArgumentException(
                "Failed to find AppDbContext. Was the class registered for DI?");
        }

        if (mapper == null)
        {
            throw new ArgumentException(
                "Failed to find IMapper. Was the class registered for DI?");
        }
    }

    public void DebugTypeMapping()
    {
        var configuration = mapper.ConfigurationProvider;
        var configurationType = configuration.GetType();
        var configuredMapsFieldInfo = configurationType.GetField("_configuredMaps", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var configuredMaps = (Dictionary<TypePair, TypeMap>)configuredMapsFieldInfo.GetValue(configuration)!;
        var allTypeMaps = configuredMaps.Values;

        foreach (var typeMap in allTypeMaps)
        {
            Console.WriteLine($"Source: {typeMap.SourceType.Name} Destination: {typeMap.DestinationType.Name}");
        }
    }

    public async Task PrepDb()
    {
        await meilisearchDbContext.CreateModelsAndInstantiateDatabase();

        await SyncTracks();
        await SyncAlbums();
    }

    public async Task SyncTracks()
    {
        // Check if the Meilisearch index is empty
        // If it is, sync all tracks

        // Get Index document count
        var stats = await meilisearchDbContext.SearchableTracks.GetStatsAsync();
        var documentCount = stats.NumberOfDocuments;

        var dbTracksCount = appDbContext.Tracks.Count();

        Console.WriteLine($"Sync Track. DB Row Count: {dbTracksCount} vs Meilisearch: {documentCount}");
        if (true)
        {
            // Configure Filterable Attributes
            var filterableAttributes = new List<string>
            {
                "OriginalTracksPk",
                "OriginalAlbumsPk",
            };

            var updateFilterableAttributesTask = await meilisearchDbContext.SearchableTracks.UpdateFilterableAttributesAsync(filterableAttributes);
            var updateFilterableAttributesTaskResult = await meilisearchDbContext.AwaitTask(updateFilterableAttributesTask.TaskUid);

            if (updateFilterableAttributesTaskResult.Status != TaskInfoStatus.Succeeded)
            {
                throw new Exception(
                    $"Failed to update filterable attributes in Meilisearch. Task returned status: {updateFilterableAttributesTask.Status} != success");
            }

            // Clear all documents
            Console.WriteLine("Syncing");
            Console.WriteLine("Deleting All Documents from Database");
            var deleteTask = await meilisearchDbContext.SearchableTracks.DeleteAllDocumentsAsync();
            var deleteTaskResult = await meilisearchDbContext.AwaitTask(deleteTask.TaskUid);
            if (deleteTaskResult.Status != TaskInfoStatus.Succeeded)
            {
                throw new Exception(
                    $"Failed to delete documents from Meilisearch. Task returned status: {deleteTask.Status} != success");
            }

            // Sync all tracks
            Console.WriteLine("Fetching All From The Database");
            var tracks = await appDbContext.Tracks
                .IgnoreAutoIncludes()
                .Include(t => t.Album)
                .Include(t => t.Original)
                .ThenInclude(a => a.Album)
                .AsNoTracking()
                .ToListAsync();

            Console.WriteLine("Mapping Tracks to Searchable Tracks");
            DebugTypeMapping();
            var searchableTracks = mapper.Map<List<Track>, List<SearchableTrack>>(tracks);
            Console.WriteLine("Adding Documents to Meilisearch");
            var task = await meilisearchDbContext.SearchableTracks.AddDocumentsAsync(searchableTracks);

            var taskResult = await meilisearchDbContext.AwaitTask(task.TaskUid);

            if (taskResult.Status != TaskInfoStatus.Succeeded)
            {
                throw new Exception($"Failed to add documents to Meilisearch. Task returned status: {task.Status} != success");
            }

            Console.WriteLine(
                $"Added {searchableTracks.Count} documents to Meilisearch. Task Status: {task.Status}");
        }
    }

    public async Task SyncAlbums()
    {
        // Check if the Meilisearch index is empty
        // If it is, sync all tracks

        // Get Index document count
        var stats = await meilisearchDbContext.SearchableAlbums.GetStatsAsync();
        var documentCount = stats.NumberOfDocuments;

        var dbTracksCount = appDbContext.Albums.Count();

        Console.WriteLine($"Sync Albums. DB Row Count: {dbTracksCount} vs Meilisearch: {documentCount}");
        if (true)
        {
            // Clear all documents
            Console.WriteLine("Syncing");
            Console.WriteLine("Deleting All Documents from Database");
            await meilisearchDbContext.SearchableAlbums.DeleteAllDocumentsAsync();

            // Sync all tracks
            Console.WriteLine("Fetching All From The Database");
            var albums = await appDbContext.Albums
                .IgnoreAutoIncludes()
                .Include(t => t.Tracks)
                .Include(t => t.AlbumArtist)
                .AsNoTracking()
                .ToListAsync();

            Console.WriteLine("Mapping Tracks to Searchable Tracks");
            var searchableTracks = mapper.Map<List<SearchableAlbum>>(albums);

            Console.WriteLine("Adding Documents to Meilisearch");
            var task = await meilisearchDbContext.SearchableAlbums.AddDocumentsAsync(searchableTracks);

            var taskResult = await meilisearchDbContext.AwaitTask(task.TaskUid);

            if (taskResult.Status != TaskInfoStatus.Succeeded)
            {
                throw new Exception(
                    $"Failed to add documents to Meilisearch. Task returned status: {task.Status} != success");
            }

            Console.WriteLine(
                $"Added {searchableTracks.Count} documents to Meilisearch. Task Status: {task.Status}");
        }
    }
}