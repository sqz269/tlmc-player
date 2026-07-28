using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Pgvector.EntityFrameworkCore;

namespace TlmcPlayerBackend.Data;

/// <summary>
/// Lets `dotnet ef` build the context without booting the full application
/// host, which eagerly fetches the Keycloak OIDC configuration and fails in
/// any environment where the SSO endpoint is unreachable.
/// </summary>
public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var connectionString =
            Environment.GetEnvironmentVariable("ConnectionStrings__PostgreSql")
            ?? "Host=localhost;Port=5432;Database=tlmcplayer;Username=tlmcplayer;Password=tlmcplayer";

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(connectionString, o => o.UseVector())
            .Options;

        return new AppDbContext(options);
    }
}
