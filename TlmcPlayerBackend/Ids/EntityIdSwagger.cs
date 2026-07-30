using Microsoft.OpenApi.Any;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace TlmcPlayerBackend.Ids;

public static class EntityIdSwagger
{
    /// <summary>
    /// Without these, the OpenAPI document describes ids as {"value": "..."} objects
    /// and generated clients break.
    /// </summary>
    public static void MapEntityIds(this SwaggerGenOptions options)
    {
        Map<ReleaseId>(options);
        Map<DiscId>(options);
        Map<TrackId>(options);
        Map<AssetId>(options);
        Map<ArtworkId>(options);
        Map<CircleId>(options);
        Map<TagId>(options);
        Map<OriginalWorkId>(options);
        Map<OriginalSongId>(options);
        Map<PlaylistId>(options);
        Map<LyricsId>(options);
        Map<UserId>(options);
    }

    private static void Map<TId>(SwaggerGenOptions options) where TId : struct, IEntityId<TId>
    {
        var example = TypeId.Format(TId.Prefix, Guid.Empty);
        options.MapType<TId>(() => new OpenApiSchema
        {
            Type = "string",
            Pattern = $"^{TId.Prefix}_[0-7][0-9a-hjkmnp-tv-z]{{25}}$",
            Example = new OpenApiString(example),
        });
        options.MapType<TId?>(() => new OpenApiSchema
        {
            Type = "string",
            Nullable = true,
            Pattern = $"^{TId.Prefix}_[0-7][0-9a-hjkmnp-tv-z]{{25}}$",
            Example = new OpenApiString(example),
        });
    }
}
