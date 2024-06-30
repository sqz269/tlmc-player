namespace TlmcPlayerBackend.Models.Api;

public class TrackFilter
{
    public string? Title { get; set; }
    public List<string>? Original { get; set; } = new();
    public List<string>? OriginalId { get; set; } = new();
    public List<string>? Staff { get; set; } = new();
}