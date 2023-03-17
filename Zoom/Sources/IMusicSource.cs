namespace Zoom.Sources;

public interface IMusicData
{
    string DirectUrl { get; }
    string Identifier { get; }

    SongInfo ToSongInfo();
}
public interface IMusicSource
{
    string Name { get; }
    string CommandName { get; }
    string Category { get; }

    Task<IMusicData> Search(string query);
}
