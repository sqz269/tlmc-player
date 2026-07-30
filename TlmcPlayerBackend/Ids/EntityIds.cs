namespace TlmcPlayerBackend.Ids;

/// <summary>
/// A 16-byte id that renders as a prefixed TypeID (`trk_...`) everywhere a human or a
/// client sees it, and stays a bare `uuid` everywhere it is stored or joined.
/// The distinct CLR types make `GetTrack(ReleaseId)` a compile error instead of a 404.
/// </summary>
public interface IEntityId<TSelf> where TSelf : struct, IEntityId<TSelf>
{
    static abstract string Prefix { get; }
    static abstract TSelf FromGuid(Guid value);
    Guid Value { get; }
}

// Each id is ~the same seven lines; the TryParse(string, IFormatProvider, out T)
// signature is what lets MVC bind them straight from route and query values.

public readonly record struct ReleaseId(Guid Value) : IEntityId<ReleaseId>
{
    public static string Prefix => "rel";
    public static ReleaseId FromGuid(Guid value) => new(value);
    public static ReleaseId New() => new(Guid.CreateVersion7());
    public override string ToString() => TypeId.Format(Prefix, Value);
    public static ReleaseId Parse(string s) => new(TypeId.Parse(Prefix, s));
    public static bool TryParse(string? s, IFormatProvider? _, out ReleaseId result)
    {
        var ok = TypeId.TryParse(Prefix, s, out var g);
        result = new ReleaseId(g);
        return ok;
    }
}

public readonly record struct DiscId(Guid Value) : IEntityId<DiscId>
{
    public static string Prefix => "dsc";
    public static DiscId FromGuid(Guid value) => new(value);
    public static DiscId New() => new(Guid.CreateVersion7());
    public override string ToString() => TypeId.Format(Prefix, Value);
    public static DiscId Parse(string s) => new(TypeId.Parse(Prefix, s));
    public static bool TryParse(string? s, IFormatProvider? _, out DiscId result)
    {
        var ok = TypeId.TryParse(Prefix, s, out var g);
        result = new DiscId(g);
        return ok;
    }
}

public readonly record struct TrackId(Guid Value) : IEntityId<TrackId>
{
    public static string Prefix => "trk";
    public static TrackId FromGuid(Guid value) => new(value);
    public static TrackId New() => new(Guid.CreateVersion7());
    public override string ToString() => TypeId.Format(Prefix, Value);
    public static TrackId Parse(string s) => new(TypeId.Parse(Prefix, s));
    public static bool TryParse(string? s, IFormatProvider? _, out TrackId result)
    {
        var ok = TypeId.TryParse(Prefix, s, out var g);
        result = new TrackId(g);
        return ok;
    }
}

public readonly record struct AssetId(Guid Value) : IEntityId<AssetId>
{
    public static string Prefix => "file";
    public static AssetId FromGuid(Guid value) => new(value);
    public static AssetId New() => new(Guid.CreateVersion7());
    public override string ToString() => TypeId.Format(Prefix, Value);
    public static AssetId Parse(string s) => new(TypeId.Parse(Prefix, s));
    public static bool TryParse(string? s, IFormatProvider? _, out AssetId result)
    {
        var ok = TypeId.TryParse(Prefix, s, out var g);
        result = new AssetId(g);
        return ok;
    }
}

public readonly record struct ArtworkId(Guid Value) : IEntityId<ArtworkId>
{
    public static string Prefix => "art";
    public static ArtworkId FromGuid(Guid value) => new(value);
    public static ArtworkId New() => new(Guid.CreateVersion7());
    public override string ToString() => TypeId.Format(Prefix, Value);
    public static ArtworkId Parse(string s) => new(TypeId.Parse(Prefix, s));
    public static bool TryParse(string? s, IFormatProvider? _, out ArtworkId result)
    {
        var ok = TypeId.TryParse(Prefix, s, out var g);
        result = new ArtworkId(g);
        return ok;
    }
}

public readonly record struct CircleId(Guid Value) : IEntityId<CircleId>
{
    public static string Prefix => "cir";
    public static CircleId FromGuid(Guid value) => new(value);
    public static CircleId New() => new(Guid.CreateVersion7());
    public override string ToString() => TypeId.Format(Prefix, Value);
    public static CircleId Parse(string s) => new(TypeId.Parse(Prefix, s));
    public static bool TryParse(string? s, IFormatProvider? _, out CircleId result)
    {
        var ok = TypeId.TryParse(Prefix, s, out var g);
        result = new CircleId(g);
        return ok;
    }
}

public readonly record struct TagId(Guid Value) : IEntityId<TagId>
{
    public static string Prefix => "tag";
    public static TagId FromGuid(Guid value) => new(value);
    public static TagId New() => new(Guid.CreateVersion7());
    public override string ToString() => TypeId.Format(Prefix, Value);
    public static TagId Parse(string s) => new(TypeId.Parse(Prefix, s));
    public static bool TryParse(string? s, IFormatProvider? _, out TagId result)
    {
        var ok = TypeId.TryParse(Prefix, s, out var g);
        result = new TagId(g);
        return ok;
    }
}

public readonly record struct OriginalWorkId(Guid Value) : IEntityId<OriginalWorkId>
{
    public static string Prefix => "work";
    public static OriginalWorkId FromGuid(Guid value) => new(value);
    public static OriginalWorkId New() => new(Guid.CreateVersion7());
    public override string ToString() => TypeId.Format(Prefix, Value);
    public static OriginalWorkId Parse(string s) => new(TypeId.Parse(Prefix, s));
    public static bool TryParse(string? s, IFormatProvider? _, out OriginalWorkId result)
    {
        var ok = TypeId.TryParse(Prefix, s, out var g);
        result = new OriginalWorkId(g);
        return ok;
    }
}

public readonly record struct OriginalSongId(Guid Value) : IEntityId<OriginalSongId>
{
    public static string Prefix => "song";
    public static OriginalSongId FromGuid(Guid value) => new(value);
    public static OriginalSongId New() => new(Guid.CreateVersion7());
    public override string ToString() => TypeId.Format(Prefix, Value);
    public static OriginalSongId Parse(string s) => new(TypeId.Parse(Prefix, s));
    public static bool TryParse(string? s, IFormatProvider? _, out OriginalSongId result)
    {
        var ok = TypeId.TryParse(Prefix, s, out var g);
        result = new OriginalSongId(g);
        return ok;
    }
}

public readonly record struct PlaylistId(Guid Value) : IEntityId<PlaylistId>
{
    public static string Prefix => "pls";
    public static PlaylistId FromGuid(Guid value) => new(value);
    public static PlaylistId New() => new(Guid.CreateVersion7());
    public override string ToString() => TypeId.Format(Prefix, Value);
    public static PlaylistId Parse(string s) => new(TypeId.Parse(Prefix, s));
    public static bool TryParse(string? s, IFormatProvider? _, out PlaylistId result)
    {
        var ok = TypeId.TryParse(Prefix, s, out var g);
        result = new PlaylistId(g);
        return ok;
    }
}

public readonly record struct LyricsId(Guid Value) : IEntityId<LyricsId>
{
    public static string Prefix => "lyr";
    public static LyricsId FromGuid(Guid value) => new(value);
    public static LyricsId New() => new(Guid.CreateVersion7());
    public override string ToString() => TypeId.Format(Prefix, Value);
    public static LyricsId Parse(string s) => new(TypeId.Parse(Prefix, s));
    public static bool TryParse(string? s, IFormatProvider? _, out LyricsId result)
    {
        var ok = TypeId.TryParse(Prefix, s, out var g);
        result = new LyricsId(g);
        return ok;
    }
}

public readonly record struct UserId(Guid Value) : IEntityId<UserId>
{
    public static string Prefix => "usr";
    public static UserId FromGuid(Guid value) => new(value);
    public override string ToString() => TypeId.Format(Prefix, Value);
    public static UserId Parse(string s) => new(TypeId.Parse(Prefix, s));
    public static bool TryParse(string? s, IFormatProvider? _, out UserId result)
    {
        var ok = TypeId.TryParse(Prefix, s, out var g);
        result = new UserId(g);
        return ok;
    }
}
