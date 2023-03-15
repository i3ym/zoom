using System.Collections;

namespace Zoom;

public class SongQueue : IEnumerable<Song>
{
    public event Action OnAdd = delegate { };

    public int Count => Queue.Count;

    Radio? _Radio;
    public Radio? Radio { get => _Radio; set { _Radio = value; OnAdd(); } }
    readonly List<Song> Queue = new();

    public Song this[int index] => Queue[index];

    public void RemoveTop() => TryRemoveAt(0);
    public bool TryRemoveAt(int index)
    {
        if (index < 0 || index >= Queue.Count) return false;

        Queue.RemoveAt(index);
        return true;
    }

    public void Enqueue(IEnumerable<Song> songs)
    {
        foreach (var song in songs)
            Enqueue(song);
    }
    public void Enqueue(Song song)
    {
        Queue.Add(song);
        OnAdd();
    }
    public void EnqueueOnTop(Song song)
    {
        Queue.Insert(0, song);
        OnAdd();
    }
    public bool TryMove(int from, int to)
    {
        if (from < 0 || to < 0) return false;
        if (from >= Queue.Count || to >= Queue.Count) return false;

        var item = Queue[from];
        Queue.RemoveAt(from);
        Queue.Insert(to, item);
        return true;
    }
    public async ValueTask<Song?> Dequeue()
    {
        if (Queue.Count != 0) return TryDequeue();
        if (Radio is { }) return await Radio.GetNext();

        return null;
    }
    public void Clear()
    {
        Queue.Clear();
        Radio = null;
    }

    Song? TryDequeue()
    {
        if (Queue.Count == 0) return null;

        var value = Queue[0];
        Queue.RemoveAt(0);

        return value;
    }

    public IEnumerator<Song> GetEnumerator() => Queue.GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}