using System.Diagnostics;

namespace Zoom.Sources;

public class YouTube
{
    public static OperationResult<Song[]> Url(string url) => Parse(url);
    public static OperationResult<Song[]> Search(string search) => Parse("ytsearch1:" + search);
    static OperationResult<Song[]> Parse(string str)
    {
        var process = Process.Start(new ProcessStartInfo("/usr/bin/yt-dlp", "--flat-playlist -f bestaudio --get-title --get-duration --get-id --cookies cookies.txt \"" + str + "\"") { RedirectStandardOutput = true })!;
        process.WaitForExit();

        var downurl = process.StandardOutput.ReadToEnd();
        if (downurl.StartsWith("ERROR")) return OperationResult.Err(downurl.Substring("ERROR".Length));

        return downurl.Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select((x, i) => (x, i / 3))
            .GroupBy(x => x.Item2)
            .Select(x => x.Select(x => x.x).ToArray())
            .Select(x => new Song(new SongInfo(x[0], ParseSeconds(x[2])), FileSoundStream.FromYoutubeId(x[1])))
            .ToArray();


        static int ParseSeconds(string sec)
        {
            if (!sec.Contains(':')) return int.Parse(sec);

            var times = sec.Split(':');
            var time = int.Parse(times[^1]);

            if (times.Length > 1) time += (int.Parse(times[^2]) * 60);
            if (times.Length > 2) time += (int.Parse(times[^3]) * 60 * 60);

            return time;
        }
    }

    public static string GetDownloadUrl(string id)
    {
        var process = Process.Start(new ProcessStartInfo("/usr/bin/yt-dlp", "-f bestaudio --get-url --cookies cookies.txt \"" + id + "\"") { RedirectStandardOutput = true })!;
        process.WaitForExit();

        var downurl = process.StandardOutput.ReadToEnd();
        if (downurl.StartsWith("ERROR")) throw new InvalidOperationException("YouTube video id \"" + id + "\" was invalid");

        return downurl;
    }
}