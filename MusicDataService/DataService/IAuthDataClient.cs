namespace MusicDataService.DataService;

public interface IAuthDataClient
{
    public Task<string?> GetPublicKey();
}