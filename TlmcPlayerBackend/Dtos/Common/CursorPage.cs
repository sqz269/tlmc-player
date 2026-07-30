namespace TlmcPlayerBackend.Dtos.Common;

/// <summary>
/// Keyset-paginated list envelope. `next` is an opaque cursor for the following
/// page, or null at the end. There is deliberately no `total` — it stops being
/// free under keyset pagination and belongs to a separate cacheable call.
/// </summary>
public class CursorPage<T>
{
    public List<T> Items { get; set; } = [];

    public string? Next { get; set; }
}
