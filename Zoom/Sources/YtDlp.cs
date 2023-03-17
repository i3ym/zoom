using System.Diagnostics;

namespace Zoom.Sources;

public static class YtDlp
{
    static readonly string Executable = "/bin/yt-dlp";

    public static async Task<string> GetUrl(string data)
    {
        var process = Process.Start(new ProcessStartInfo(Executable, "-f bestaudio --get-url --cookies cookies.txt \"" + data + "\"") { RedirectStandardOutput = true })!;
        await process.WaitForExitAsync();

        var downurl = await process.StandardOutput.ReadToEndAsync();
        if (downurl.StartsWith("ERROR"))
            throw new InvalidOperationException(downurl);

        return downurl.Trim();
    }

    // yt-dlp --flat-playlist -f bestaudio --get-title --get-duration --get-id 'mixcloud:otographic_kenji-sekiguchi-nhato-otographic-arts-159-2023-03-07'
}
