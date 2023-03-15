namespace Zoom.Sources;

public abstract class Radio
{
    public abstract string Name { get; }

    readonly Queue<Song> Queue = new Queue<Song>();

    public Task<Song> GetNext() => LoadNext();
    protected abstract Task<Song> LoadNext();
}