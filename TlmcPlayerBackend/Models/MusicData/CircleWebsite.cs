using TlmcPlayerBackend.Ids;

namespace TlmcPlayerBackend.Models.MusicData;

public class CircleWebsite
{
    public Guid Id { get; set; }

    public string Url { get; set; } = null!;

    // Indicates if the Website is not longer valid
    // but may need to be kept for historical reasons
    public bool Invalid { get; set; }

    public CircleId CircleId { get; set; }
    public Circle Circle { get; set; } = null!;
}
