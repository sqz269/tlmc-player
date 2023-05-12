using System.Runtime.InteropServices;
using ClientApi.MusicDataServiceClientApi.Model;

namespace RadioService;

public class RadioSongProviderService
{
    private RadioSong? _song;
    private readonly Queue<TrackReadDto> _songList;

    // use one lock for now for both song and songList, might change to two later
    private readonly ReaderWriterLockSlim _lock;

    public RadioSongProviderService()
    {
        _lock = new ReaderWriterLockSlim();
        _songList = new Queue<TrackReadDto>();
    }

    public RadioSong? GetCurrent(out string? error)
    {
        error = null;

        // Immanently acquire a read lock
        if (!_lock.TryEnterReadLock(TimeSpan.FromSeconds(2)))
        {
            error = "Resource read operation timeout (2s)";
        }

        if (_song == null && _songList.Count == 0)
        {
            error = "No songs available";
            return null;
        }

        try
        {
            if (_song != null && _song.Value.StartTime.Add(_song.Value.Duration) > DateTime.UtcNow)
            {
                return _song;
            }
        }
        finally
        {
            _lock.ExitReadLock();
        }

        // Read lock is dropped here
        // Acquire new write lock
        if (!_lock.TryEnterWriteLock(TimeSpan.FromSeconds(2)))
        {
            error = "Resource write operation timeout (2s)";
            return null;
        }

        try
        {
            // pop from queue
            var data = _songList.Dequeue();

            _song = new RadioSong
            {
                TrackId = data.Id,
                Duration = TimeSpan.Parse(data.Duration),
                StartTime = DateTime.UtcNow,
                Track = data,
            };

            return _song;
        }
        finally
        {
            _lock.ExitWriteLock();
        }
    }

    public int? GetQueueCount(out string? error)
    {
        error = null;

        if (!_lock.TryEnterReadLock(TimeSpan.FromSeconds(2)))
        {
            error = "Failed to acquire read lock in a timely manner (2s)";
            return null;
        }

        try
        {
            return _songList.Count;
        }
        finally
        {
            _lock.ExitReadLock();
        }
    }

    public bool AddToList(TrackReadDto track, out string? error)
    {
        error = null;
        if (!_lock.TryEnterWriteLock(TimeSpan.FromSeconds(2)))
        {
            error = "Failed to acquire write lock in a timely manner (2s)";
            return false;
        }

        try
        {
            _songList.Enqueue(track);
            return true;
        }
        finally
        {
            _lock.ExitWriteLock();
        }
    }

    public bool AddToList(List<TrackReadDto> track, out string? error)
    {
        error = null;
        if (!_lock.TryEnterWriteLock(TimeSpan.FromSeconds(2)))
        {
            error = "Failed to acquire write lock in a timely manner (2s)";
            return false;
        }

        try
        {
            track.ForEach(_songList.Enqueue);
            return true;
        }
        finally
        {
            _lock.ExitWriteLock();
        }
    }
}