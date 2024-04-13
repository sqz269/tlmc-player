using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TlmcPlayerBackend.Models.MusicData;

public class CircleWebsite
{
    [Key]
    public Guid Id { get; set; }

    public string Url { get; set; }

    // Indicates if the Website is not longer valid
    // but may need to be kept for historical reasons
    public bool Invalid { get; set; }

    public Circle Circle { get; set; }
}