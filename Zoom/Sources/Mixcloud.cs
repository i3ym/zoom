using System.Net.Http.Json;
using System.Text.Json.Serialization;
using System.Web;

namespace Zoom.Sources;

public class Mixcloud : IMusicSource
{
    string IMusicSource.Name => "MixCloud";
    string IMusicSource.CommandName => "mix";
    string IMusicSource.Category => "mixcloud";


    public Task<ImmutableArray<SongDataInfo>> GetByUrl(string url) => YtDlp.GetInfo(url);
    public Task<string> GetDirectUrl(string data) => YtDlp.GetUrl(data);

    public async Task<ImmutableArray<SongDataInfo>> Search(string query)
    {
        var search = await SearchMix(query);
        return search.Data
            //.Take(IMusicSource.MaxSearch)
            .Select(e => new SongDataInfo(e.Slug, e.Name, e.AudioLength))
            .ToImmutableArray();
    }


    public async Task<SearchResults> SearchMix(string query) =>
        await Http.Instance.GetFromJsonAsync<SearchResults>($"https://api.mixcloud.com/search/?type=cloudcast&q={HttpUtility.UrlEncode(query)}").ThrowIfNull();


    public record SearchResults(ImmutableArray<SearchResult> Data, SearchResultsPaging Paging);
    public record SearchResultsPaging(string Next);

    public record SearchResult(SearchResultUser User, string Name, string Url, string Slug, [property: JsonPropertyName("audio_length")] int AudioLength)
    {
        public SongInfo ToSongInfo() => SongInfo.Create(User.Name, Name, AudioLength);
    }
    public record SearchResultUser(string Name);


    /*public async Task<Zoom.Sources.SearchResult> Searchc(string query)
    {
        var results = await Http.Instance.GetFromJsonAsync<SearchResults>($"https://api.mixcloud.com/search/?type=cloudcast&q={HttpUtility.UrlEncode(query)}").ThrowIfNull();

    }

    Task<MusicData> IMusicSource.GetByUrl(string url) => YtDlp.GetUrl(url);
    public Task<string> GetDirectUrl(string data) => YtDlp.GetUrl(data);


    async Task<ImmutableArray<MusicData>> IMusicSource.Search(string query)
    {
        var data = (await Search(query)).Data[0];
        return ImmutableArray.Create(new MusicData(data.Slug, data.Url, data.ToSongInfo()));
    }*/
}
