using AuthenticationService.SyncDataService.Grpc.Proto;
using AuthServiceClientApi.KeyProviders;
using Grpc.Core;

namespace AuthenticationService.SyncDataService.Grpc;

public class GrpcAuthDataService : GrpcAuthenticationService.GrpcAuthenticationServiceBase
{
    private readonly IJwtKeyProvider _jwtKeyProvider;

    public GrpcAuthDataService(IJwtKeyProvider jwtKeyProvider)
    {
        _jwtKeyProvider = jwtKeyProvider;
    }

    public override Task<JwtPublicKeyResponse> GetJwtPublicKey(GetJwtPublicKeyRequest request, ServerCallContext context)
    {
        return Task.FromResult(new JwtPublicKeyResponse
        {
            PublicKey = _jwtKeyProvider.GetJwtRsPublicKey()
        });
    }
}