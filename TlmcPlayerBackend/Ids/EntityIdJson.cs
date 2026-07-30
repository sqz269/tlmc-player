using Newtonsoft.Json;

namespace TlmcPlayerBackend.Ids;

/// <summary>
/// Newtonsoft converter (this project serializes through AddNewtonsoftJson, not
/// System.Text.Json) rendering ids as their prefixed TypeID strings on the wire.
/// Handles both TId and TId? so nullable FK properties serialize as string-or-null.
/// </summary>
public class EntityIdJsonConverter<TId> : JsonConverter where TId : struct, IEntityId<TId>
{
    public override bool CanConvert(Type objectType)
        => objectType == typeof(TId) || objectType == typeof(TId?);

    public override void WriteJson(JsonWriter writer, object? value, JsonSerializer serializer)
    {
        if (value is TId id)
        {
            writer.WriteValue(id.ToString());
        }
        else
        {
            writer.WriteNull();
        }
    }

    public override object? ReadJson(JsonReader reader, Type objectType, object? existingValue, JsonSerializer serializer)
    {
        if (reader.TokenType == JsonToken.Null)
        {
            return objectType == typeof(TId?)
                ? null
                : throw new JsonSerializationException($"Null is not a valid {typeof(TId).Name}.");
        }

        if (reader.Value is not string s || !TypeId.TryParse(TId.Prefix, s, out var g))
        {
            throw new JsonSerializationException(
                $"'{reader.Value}' is not a valid '{TId.Prefix}_' TypeID.");
        }

        return TId.FromGuid(g);
    }
}

public static class EntityIdJson
{
    public static void AddEntityIdConverters(this JsonSerializerSettings settings)
    {
        settings.Converters.Add(new EntityIdJsonConverter<ReleaseId>());
        settings.Converters.Add(new EntityIdJsonConverter<DiscId>());
        settings.Converters.Add(new EntityIdJsonConverter<TrackId>());
        settings.Converters.Add(new EntityIdJsonConverter<AssetId>());
        settings.Converters.Add(new EntityIdJsonConverter<ArtworkId>());
        settings.Converters.Add(new EntityIdJsonConverter<CircleId>());
        settings.Converters.Add(new EntityIdJsonConverter<TagId>());
        settings.Converters.Add(new EntityIdJsonConverter<OriginalWorkId>());
        settings.Converters.Add(new EntityIdJsonConverter<OriginalSongId>());
        settings.Converters.Add(new EntityIdJsonConverter<PlaylistId>());
        settings.Converters.Add(new EntityIdJsonConverter<LyricsId>());
        settings.Converters.Add(new EntityIdJsonConverter<UserId>());
    }
}
