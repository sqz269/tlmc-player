using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Pgvector.EntityFrameworkCore;
using TlmcPlayerBackend.Models.MusicData;
using TlmcPlayerBackend.Models.Playlist;

namespace TlmcPlayerBackend.Data;

/// <summary>
/// One place for the Npgsql/EF wiring so Program and the design-time factory cannot
/// drift: snake_case identifiers, snake_case jsonb document keys (they are part of
/// the schema — the generated name_sort columns read name->>'default'), the postgres
/// enums, and pgvector.
/// </summary>
public static class AppDbOptions
{
    public static NpgsqlDataSource BuildDataSource(string? connectionString)
    {
        var dataSourceBuilder = new NpgsqlDataSourceBuilder(connectionString);

        // jsonb key casing is controlled here, by the serializer options handed to
        // dynamic JSON — not by the EF naming convention.
        dataSourceBuilder.EnableDynamicJson();
        dataSourceBuilder.ConfigureJsonOptions(new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        });

        dataSourceBuilder.UseVector();

        dataSourceBuilder.MapEnum<StorageRoot>("storage_root");
        dataSourceBuilder.MapEnum<CreditRole>("credit_role");
        dataSourceBuilder.MapEnum<PlaylistKind>("playlist_kind");
        dataSourceBuilder.MapEnum<PlaylistVisibility>("playlist_visibility");
        dataSourceBuilder.MapEnum<PlaySource>("play_source");

        return dataSourceBuilder.Build();
    }

    public static DbContextOptionsBuilder Configure(
        DbContextOptionsBuilder options, NpgsqlDataSource dataSource, int commandTimeoutSeconds = 30)
    {
        return options
            .UseNpgsql(dataSource, o =>
            {
                o.CommandTimeout(commandTimeoutSeconds);
                o.UseVector();
                o.MapEnum<StorageRoot>("storage_root");
                o.MapEnum<CreditRole>("credit_role");
                o.MapEnum<PlaylistKind>("playlist_kind");
                o.MapEnum<PlaylistVisibility>("playlist_visibility");
                o.MapEnum<PlaySource>("play_source");
            })
            .UseSnakeCaseNamingConvention();
    }
}
