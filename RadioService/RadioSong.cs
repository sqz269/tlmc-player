using ClientApi.MusicDataServiceClientApi.Model;

namespace RadioService;

public struct RadioSong
{
    public Guid TrackId { get; init; }
    public TrackReadDto Track { get; init; }
    public TimeSpan Duration { get; init; }
    public DateTime StartTime { get; init; }

    public TimeSpan? ElapsedTime { get; set; }
}
