namespace Veya.Shared.Notifications;

/// <summary>
/// In-memory <see cref="INotificationStore"/> (ADR-0011): a bounded ring of the
/// most recent notifications. Thread-safe because the capture service writes
/// while a digest reads. Transient by design — a daemon restart clears it;
/// persistence is a deferred follow-up behind the same interface.
/// </summary>
public sealed class InMemoryNotificationStore : INotificationStore
{
    private readonly int _capacity;
    private readonly LinkedList<Notification> _items = new();
    private readonly Lock _gate = new();

    /// <param name="capacity">Maximum notifications retained; the oldest are evicted past this.</param>
    public InMemoryNotificationStore(int capacity = 200)
    {
        if (capacity <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(capacity), "Capacity must be positive.");
        }

        _capacity = capacity;
    }

    public int Count
    {
        get
        {
            lock (_gate)
            {
                return _items.Count;
            }
        }
    }

    public void Add(Notification notification)
    {
        lock (_gate)
        {
            // Newest at the front; evict from the back when full.
            _items.AddFirst(notification);
            while (_items.Count > _capacity)
            {
                _items.RemoveLast();
            }
        }
    }

    public IReadOnlyList<Notification> GetRecent(int count)
    {
        if (count <= 0)
        {
            return [];
        }

        lock (_gate)
        {
            return _items.Take(count).ToList();
        }
    }

    public IReadOnlyList<Notification> GetByApp(string appName)
    {
        lock (_gate)
        {
            return _items.Where(item => string.Equals(item.AppName, appName, StringComparison.OrdinalIgnoreCase)).ToList();
        }
    }
}
