namespace Zoom.Sources;

public record SongDataInfo(string Id, string Title, double LengthSec);
public interface IMusicSource
{
    public const int MaxSearch = 5;

    string Name { get; }
    string CommandName { get; }
    string Category { get; }


    Task<ImmutableArray<SongDataInfo>> Search(string query);
    Task<ImmutableArray<SongDataInfo>> GetByUrl(string url);

    Task<string> GetDirectUrl(string data);
}