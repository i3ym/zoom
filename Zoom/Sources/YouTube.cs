namespace Zoom.Sources;

public class YouTube : IMusicSource
{
    string IMusicSource.Name => "YouTube";
    string IMusicSource.CommandName => "yt";
    string IMusicSource.Category => "youtube";


    public Task<ImmutableArray<SongDataInfo>> GetByUrl(string url) => YtDlp.GetInfo(url);
    public Task<string> GetDirectUrl(string data) => YtDlp.GetUrl(data);
    public Task<ImmutableArray<SongDataInfo>> Search(string query) => YtDlp.GetInfo($"ytsearch{IMusicSource.MaxSearch}:{query}");
}