namespace Zoom;

public abstract class SoundStream
{
    public const int BytesPerSec = Decoder.SampleRate * Decoder.Bits / 8 * Decoder.Channels;

    public long LoadedSeconds => Loaded / BytesPerSec;
    public long ProgressSeconds { get => Position / BytesPerSec; set => Position = Math.Max(value * BytesPerSec, 0); }
    public abstract long Loaded { get; }
    public abstract long Position { get; set; }

    public bool IsFullyLoaded { get; private set; }

    public readonly CancellationTokenSource CancellationToken = new CancellationTokenSource();

    protected abstract Func<Task>? LoadFunc { get; set; }

    public void StartLoading()
    {
        if (LoadFunc is null) return;

        var func = LoadFunc;
        LoadFunc = null;

        func().ContinueWith(_ => IsFullyLoaded = true);
    }

    public abstract int Read(byte[] buffer);
}
public class FileSoundStream : SoundStream
{
    public override long Loaded => Reader.Length;
    public override long Position { get => Reader.Position; set => Reader.Position = value; }

    public readonly Stream Reader;
    protected override Func<Task>? LoadFunc { get; set; }

    private FileSoundStream(string identifier, Func<Stream, CancellationToken, Task> loadFunc)
    {
        var path = FileCache.TryGetCached(identifier);
        var cached = path is { };
        path ??= FileCache.GetNewPath(identifier);

        Reader = File.Open(path, FileMode.OpenOrCreate, FileAccess.Read, FileShare.ReadWrite);


        LoadFunc = () =>
        {
            if (cached) return Task.CompletedTask;

            return Task.Run(() =>
            {
                var writer = File.Open(path, FileMode.OpenOrCreate, FileAccess.Write, FileShare.ReadWrite);
                loadFunc(writer, CancellationToken.Token).Wait();

                writer.Close();
                FileCache.AddCached(identifier, path);
            });
        };
    }

    public override int Read(byte[] buffer) => Reader.Read(buffer);


    public static SoundStream FromStream(string identifier, Stream input) => new FileSoundStream(identifier, (writer, token) => input.CopyToAsync(writer, token));
    public static SoundStream FromUrl(string identifier, string url) => new FileSoundStream(identifier, (writer, token) => Decoder.FromUrlProc(url, token, writer));
    public static SoundStream FromUrl(string url) => FromUrl(url, url);
    public static SoundStream FromYoutubeId(string id) => new FileSoundStream("yt_" + id, (writer, token) => Decoder.FromUrlProc(YouTube.GetDownloadUrl(id), token, writer));
}
public class DataCacheFileSoundStream : SoundStream
{
    public override long Loaded => Reader.Length;
    public override long Position { get => Reader.Position; set => Reader.Position = value; }

    public readonly Stream Reader;
    protected override Func<Task>? LoadFunc { get; set; }

    private DataCacheFileSoundStream(string category, string identifier, Func<Stream, CancellationToken, Task> loadFunc)
    {
        var path = Path.Combine(DataCache.CacheDirectory, Guid.NewGuid().ToString());
        Reader = File.Open(path, FileMode.OpenOrCreate, FileAccess.Read, FileShare.ReadWrite);


        LoadFunc = async () =>
        {
            var writer = File.Open(path, FileMode.OpenOrCreate, FileAccess.Write, FileShare.ReadWrite);
            await loadFunc(writer, CancellationToken.Token);

            writer.Close();
            DataCache.SetSongCacheFile(category, identifier, Path.GetRelativePath(DataCache.CacheDirectory, path));
        };
    }

    public override int Read(byte[] buffer) => Reader.Read(buffer);


    public static SoundStream FromUrl(string category, string identifier, string url) => new DataCacheFileSoundStream(category, identifier, (writer, token) => Decoder.FromUrlProc(url, token, writer));
}
public class CachedFileSoundStream : SoundStream
{
    public override long Loaded => Input.Length;
    public override long Position { get => Input.Position; set => Input.Position = value; }

    readonly Stream Input;
    protected override Func<Task>? LoadFunc { get; set; }

    private CachedFileSoundStream(Func<CancellationToken, Stream> func)
    {
        Input = func(CancellationToken.Token);
        LoadFunc = () => Task.CompletedTask;
    }

    public override int Read(byte[] buffer) => Input.Read(buffer);


    public static SoundStream FromPath(string path) => new CachedFileSoundStream(token => File.OpenRead(path));
    public static SoundStream FromStream(Stream input) => new CachedFileSoundStream(token => input);
}

public class MemorySoundStream : SoundStream
{
    public override long Loaded => Data.Count;
    public override long Position { get; set; }

    readonly List<byte> Data = new List<byte>();
    protected override Func<Task>? LoadFunc { get; set; }

    private MemorySoundStream(Func<List<byte>, CancellationToken, Task> loadFunc) => LoadFunc = () => Task.Run(() => loadFunc(Data, CancellationToken.Token));

    public override int Read(byte[] buffer)
    {
        var length = Math.Min(Data.Count - (int) Position, buffer.Length);
        Data.CopyTo((int) Position, buffer, 0, length);
        Position += length;

        return length;
    }


    public static SoundStream FromStream(Stream input) => new MemorySoundStream((data, token) => Decoder.FromStreamProc(input, token, data));
    public static SoundStream FromUrl(string url) => new MemorySoundStream((data, token) => Decoder.FromUrlProc(url, token, data));
}
public class PassthroughSoundStream : SoundStream
{
    public override long Loaded => int.MaxValue;
    public override long Position { get; set; }

    readonly Stream Input;
    protected override Func<Task>? LoadFunc { get; set; }

    private PassthroughSoundStream(Func<CancellationToken, Stream> func)
    {
        Input = func(CancellationToken.Token);
        LoadFunc = () => Task.Delay(-1);
    }

    public override int Read(byte[] buffer) => Input.Read(buffer);


    public static SoundStream FromUri(string uri) => new PassthroughSoundStream(token => Decoder.StreamFromUri(uri, token));
    public static SoundStream FromStream(Stream input) => new PassthroughSoundStream(token => Decoder.StreamFromStream(input, token));
}