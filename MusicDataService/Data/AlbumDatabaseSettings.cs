namespace MusicDataService.Data;

public class AlbumDatabaseSettings : IAlbumDatabaseSettings
{
    public string ConnectionString { get; set; }
    public string DatabaseName { get; set; }
    public string CollectionName { get; set; }
}