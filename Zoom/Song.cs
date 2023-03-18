namespace Zoom;

public record Song(SongInfo Info, SoundStream Stream);
public record SongInfo(string? Title, double LengthSeconds)
{
    public static SongInfo Create(string author, string title, double lengthSeconds) => new(GetTitle(author, title), lengthSeconds);

    public override string ToString() => Title ?? "<нет имени>";

    public static string GetTitle(string author, string title) => author + ": " + title;
}