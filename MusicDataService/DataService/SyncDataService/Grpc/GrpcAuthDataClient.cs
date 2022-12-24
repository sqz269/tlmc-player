using AuthenticationService.SyncDataService.Grpc.Proto;
using Grpc.Net.Client;
using static AuthenticationService.SyncDataService.Grpc.Proto.GrpcAuthenticationService;

namespace MusicDataService.DataService.SyncDataService.Grpc;

public class GrpcAuthDataClient : IAuthDataClient
{
    private readonly GrpcAuthenticationServiceClient _client;

    public GrpcAuthDataClient(IConfiguration configuration)
    {
        var channel = GrpcChannel.ForAddress(configuration["AuthDataService:GrpcUrl"]);
        _client = new GrpcAuthenticationServiceClient(channel);
    }

    public async Task<string?> GetPublicKey()
    {
        var request = new GetJwtPublicKeyRequest();

        try
        {
            var resp = await _client.GetJwtPublicKeyAsync(request);
            return resp.PublicKey;
        }
        catch (Exception e)
        {
            Console.WriteLine($"Failed to make gRPC request: {e.Message}");
            return null;
        }
    }
}