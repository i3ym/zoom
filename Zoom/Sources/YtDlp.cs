using System.Diagnostics;

namespace Zoom.Sources;

public static class YtDlp
{
    static readonly string Executable = "/bin/yt-dlp";

    public static async Task<string> Start(string args)
    {
        var process = Process.Start(new ProcessStartInfo(Executable, args) { RedirectStandardOutput = true })!;
        await process.WaitForExitAsync();

        var response = await process.StandardOutput.ReadToEndAsync();
        if (response.StartsWith("ERROR"))
            throw new InvalidOperationException(response);

        return response.Trim();
    }
    public static async Task<T[]> JStart<T>(string args) => System.Text.Json.JsonSerializer.Deserialize<T[]>("[" + (await Start(args + " -j")).TrimEnd().Replace('\n', ',') + "]").ThrowIfNull();

    public static async Task<ImmutableArray<SongDataInfo>> GetInfo(string str)
    {
        var data = await JStart<Info>($"--flat-playlist --cookies cookies.txt \"{str}\"");
        return data
            .Where(e => e.duration is not null)
            .Select(e => new SongDataInfo(e.id, e.title, e.duration!.Value))
            .ToImmutableArray();
    }
    record Info(string id, string title, double? duration = null);

    public static Task<string> GetUrl(string data) => Start($"-f bestaudio --get-url --cookies cookies.txt \"{data}\"");
}
