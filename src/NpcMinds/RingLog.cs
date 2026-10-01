namespace NpcMinds;

/// <summary>
/// A fixed-size, thread-safe log that keeps the newest <c>capacity</c> items. Any thread may add
/// (the game thread adds feed items, model workers add calls) and any thread may read a copy
/// (the viewer's server). Every add gets a rising sequence number so the page can tell new
/// items from old ones.
/// </summary>
public sealed class RingLog<T>
{
    private readonly object _gate = new();
    private readonly Queue<T> _items = new();
    private readonly int _capacity;
    private long _seq;

    public RingLog(int capacity)
        => _capacity = Math.Max(1, capacity);

    public int Capacity => _capacity;

    /// <summary>Adds the item built from the next sequence number and returns it.</summary>
    public T Add(Func<long, T> make)
    {
        lock (_gate)
        {
            T item = make(++_seq);
            _items.Enqueue(item);
            while (_items.Count > _capacity)
                _items.Dequeue();
            return item;
        }
    }

    /// <summary>A copy of the kept items, newest first.</summary>
    public IReadOnlyList<T> Newest()
    {
        lock (_gate)
        {
            T[] copy = _items.ToArray();
            Array.Reverse(copy);
            return copy;
        }
    }

    public void Clear()
    {
        lock (_gate)
            _items.Clear();
    }
}
