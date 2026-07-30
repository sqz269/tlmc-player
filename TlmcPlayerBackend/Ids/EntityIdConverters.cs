using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace TlmcPlayerBackend.Ids;

// EF value converters keeping every id a bare `uuid` column. One concrete class per id
// type because ValueConverter wants expression trees, which cannot call static abstract
// interface members.

public sealed class ReleaseIdConverter() : ValueConverter<ReleaseId, Guid>(id => id.Value, g => new ReleaseId(g));
public sealed class DiscIdConverter() : ValueConverter<DiscId, Guid>(id => id.Value, g => new DiscId(g));
public sealed class TrackIdConverter() : ValueConverter<TrackId, Guid>(id => id.Value, g => new TrackId(g));
public sealed class AssetIdConverter() : ValueConverter<AssetId, Guid>(id => id.Value, g => new AssetId(g));
public sealed class ArtworkIdConverter() : ValueConverter<ArtworkId, Guid>(id => id.Value, g => new ArtworkId(g));
public sealed class CircleIdConverter() : ValueConverter<CircleId, Guid>(id => id.Value, g => new CircleId(g));
public sealed class TagIdConverter() : ValueConverter<TagId, Guid>(id => id.Value, g => new TagId(g));
public sealed class OriginalWorkIdConverter() : ValueConverter<OriginalWorkId, Guid>(id => id.Value, g => new OriginalWorkId(g));
public sealed class OriginalSongIdConverter() : ValueConverter<OriginalSongId, Guid>(id => id.Value, g => new OriginalSongId(g));
public sealed class PlaylistIdConverter() : ValueConverter<PlaylistId, Guid>(id => id.Value, g => new PlaylistId(g));
public sealed class LyricsIdConverter() : ValueConverter<LyricsId, Guid>(id => id.Value, g => new LyricsId(g));
public sealed class UserIdConverter() : ValueConverter<UserId, Guid>(id => id.Value, g => new UserId(g));
public sealed class ApiKeyIdConverter() : ValueConverter<ApiKeyId, Guid>(id => id.Value, g => new ApiKeyId(g));

public static class EntityIdConventions
{
    /// <summary>Registers every id type's converter; called from ConfigureConventions.</summary>
    public static void Apply(ModelConfigurationBuilder builder)
    {
        builder.Properties<ReleaseId>().HaveConversion<ReleaseIdConverter>();
        builder.Properties<DiscId>().HaveConversion<DiscIdConverter>();
        builder.Properties<TrackId>().HaveConversion<TrackIdConverter>();
        builder.Properties<AssetId>().HaveConversion<AssetIdConverter>();
        builder.Properties<ArtworkId>().HaveConversion<ArtworkIdConverter>();
        builder.Properties<CircleId>().HaveConversion<CircleIdConverter>();
        builder.Properties<TagId>().HaveConversion<TagIdConverter>();
        builder.Properties<OriginalWorkId>().HaveConversion<OriginalWorkIdConverter>();
        builder.Properties<OriginalSongId>().HaveConversion<OriginalSongIdConverter>();
        builder.Properties<PlaylistId>().HaveConversion<PlaylistIdConverter>();
        builder.Properties<LyricsId>().HaveConversion<LyricsIdConverter>();
        builder.Properties<UserId>().HaveConversion<UserIdConverter>();
        builder.Properties<ApiKeyId>().HaveConversion<ApiKeyIdConverter>();
    }
}
