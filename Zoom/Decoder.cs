using System.Diagnostics;

namespace Zoom;

public static class Decoder
{
    public const int SampleRate = 48000;
    public const int Bits = 16;
    public const int Channels = 2;

    public static Task FromUrlProc(string uri, CancellationToken token, Stream addTo) => FromUrlProc(uri, token, (buffer, read) => addTo.Write(buffer, 0, read), addTo.Flush);
    public static Task FromUrlProc(string uri, CancellationToken token, List<byte> addTo) =>
        FromUrlProc(uri, token, (buffer, read) =>
        {
            if (read == buffer.Length) addTo.AddRange(buffer);
            else addTo.AddRange(buffer.Take(read));
        }, null);
    public static Task FromUrlProc(string uri, CancellationToken token, Action<byte[], int> addFunc, Action? onEnd) =>
        Task.Run(() =>
        {
            var proc = FFMpegString("\"" + uri + "\"", token);

            try
            {
                var buffer = new byte[1024 * 8];
                var stream = proc.StandardOutput.BaseStream;
                while (!proc.HasExited)
                {
                    var read = stream.Read(buffer);
                    addFunc(buffer, read);
                }

                onEnd?.Invoke();
            }
            catch (Exception ex) { Console.WriteLine(ex); }
        });
    public static Task FromStreamProc(Stream input, CancellationToken token, List<byte> addTo) =>
        Task.Run(() =>
        {
            var proc = FFMpegString("pipe:1", token);

            Task.Run(() =>
            {
                var buffer = new byte[1024 * 8];
                var writer = proc.StandardInput.BaseStream;

                while (!proc.HasExited)
                {
                    var read = input.Read(buffer);
                    writer.Write(buffer, 0, read);
                }

                writer.Flush();
            });

            try
            {
                var buffer = new byte[1024 * 8];
                var reader = proc.StandardOutput.BaseStream;

                while (!proc.HasExited)
                {
                    var read = reader.Read(buffer);

                    if (read == buffer.Length) addTo.AddRange(buffer);
                    else addTo.AddRange(buffer.Take(read));
                }
            }
            catch (Exception ex) { Console.WriteLine(ex); }
        });
    public static Stream StreamFromStream(Stream input, CancellationToken token)
    {
        var proc = FFMpegString("pipe:1", token);

        Task.Run(() =>
        {
            var buffer = new byte[1024 * 8];
            var writer = proc.StandardInput.BaseStream;

            while (!proc.HasExited)
            {
                var read = input.Read(buffer);
                writer.Write(buffer, 0, read);
            }

            writer.Flush();
        });

        return proc.StandardOutput.BaseStream;
    }
    public static Stream StreamFromUri(string uri, CancellationToken token) => FFMpegString("\"" + uri + "\"", token).StandardOutput.BaseStream;

    static Process FFMpegString(string input, CancellationToken token)
    {
        var proc = Process.Start(new ProcessStartInfo("/usr/bin/ffmpeg", $"-loglevel 0 -hide_banner -i {input} -ac {Channels} -ar {SampleRate} -f s{Bits}le pipe:1") { RedirectStandardOutput = true, RedirectStandardInput = true })!;
        token.UnsafeRegister(_ => proc.Kill(), proc);

        return proc;
    }


    public static Task<SongInfo> InfoFromUri(string path, string? possibleTitle = null) =>
        Task.Run(() =>
        {
            var process = Process.Start(new ProcessStartInfo("/usr/bin/ffprobe", "-v quiet -print_format json -show_format \"" + path + "\"") { RedirectStandardOutput = true })!;
            process.WaitForExit();

            JToken? json = JObject.Parse(process.StandardOutput.ReadToEnd());
            var format = json?["format"];
            var tags = format?["tags"];

            var author = tags?["artist"]?.Value<string>();
            var title = tags?["title"]?.Value<string>();
            var length = (int) (format?["duration"]?.Value<float>() ?? -1);

            return (author, title) switch
            {
                ({ } a, { } t) => new SongInfo(a, t, length),
                ({ } a, null) => new SongInfo(a, "Неизвестный трек", length),
                (null, { } t) => new SongInfo(t, length),

                _ when path.StartsWith('/') && (Path.GetFileNameWithoutExtension(path) is { } name and not "audio") => new SongInfo(name, length),
                _ when possibleTitle is { } => new SongInfo(possibleTitle, length),
                _ => new SongInfo(null, length),
            };
        });
}