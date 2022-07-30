using MongoDB.Driver;
using MusicDataService.Models;

namespace MusicDataService.Data;

public class AlbumRepo : IAlbumRepo
{
    private IMongoDatabase _database;
    private IMongoCollection<Album> _albums;

    public AlbumRepo(IAlbumDatabaseSettings databaseSettings)
    {
        var client = new MongoClient(databaseSettings.ConnectionString);
        _database = client.GetDatabase(databaseSettings.DatabaseName);
        _albums = _database.GetCollection<Album>(databaseSettings.CollectionName);
    }

    public Album GetAlbum(Guid id)
    {
        return _albums.Find(album => album.Id == id).FirstOrDefault();
    }

    public void UpdateAlbumData(Guid id, Album album)
    {
        _albums.UpdateOne(a => a.Id == id, new ObjectUpdateDefinition<Album>(album));
    }

    public Guid AddAlbum(Album album)
    {
        album.Id = album.Id == Guid.Empty ? Guid.NewGuid() : album.Id;

        _albums.InsertOne(album);

        return album.Id;
    }

    public void AddTrackToAlbum(Guid albumId, Track track)
    {
        var updateDef = Builders<Album>.Update.Push(a => a.Tracks, track);

        _albums.UpdateOne(a => a.Id == albumId, updateDef);
    }

    public Track GetTrack(Guid id)
    {
        throw new NotImplementedException();
    }

    public void UpdateTrackData(Track track)
    {
        throw new NotImplementedException();
    }
}