using Discord.Audio;

namespace Zoom;

public class PlayState
{
    public long LoadedSeconds => InputStream.LoadedSeconds;
    public long ProgressSeconds { get => InputStream.ProgressSeconds; set => InputStream.ProgressSeconds = value; }

    public bool IsPaused = true;
    public bool Loop = false;

    public readonly SoundStream InputStream;
    public readonly SongInfo SongInfo;

    public PlayState(SoundStream input, SongInfo info)
    {
        InputStream = input;
        SongInfo = info;
    }

    public AudioStream? Stream;
    Task? PlayTask;
    public async Task Start(AudioOutStream stream)
    {
        Stream = stream;
        if (PlayTask is { }) throw new InvalidOperationException("Player already started");

        await (PlayTask = StartPlayTask());
        if (Stream is { }) await Stream.FlushAsync();
    }
    Task StartPlayTask() => Task.Run(() =>
    {
        InputStream.StartLoading();

        var buffer = new byte[SoundStream.BytesPerSec];
        IsPaused = false;

        while (true)
        {
            if (InputStream.CancellationToken.IsCancellationRequested) return;
            if (InputStream.IsFullyLoaded && InputStream.Loaded - 1 <= InputStream.Position)
            {
                if (!Loop) return;
                ProgressSeconds = 0;
            }
            if (IsPaused || InputStream.Loaded - 1 <= InputStream.Position)
            {
                Thread.Sleep(100);
                continue;
            }

            var read = InputStream.Read(buffer);

            try { Stream?.Write(buffer, 0, read); }
            catch (OperationCanceledException) { IsPaused = true; }
        }
    });
    public void Reconnect(AudioOutStream stream) => Stream = stream;

    public void Stop() => InputStream.CancellationToken.Cancel();
}