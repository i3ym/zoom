using System.Net.Http.Json;
using System.Text.Json.Serialization;
using System.Web;

namespace Zoom.Sources;

public class Mixcloud : IMusicSource
{
    string IMusicSource.Name => "MixCloud";
    string IMusicSource.CommandName => "mix";
    string IMusicSource.Category => "mixcloud";


    async Task<IMusicData> IMusicSource.Search(string query) => (await Search(query)).Data[0];

    public async Task<SearchResults> Search(string query) =>
        await Http.Instance.GetFromJsonAsync<SearchResults>($"https://api.mixcloud.com/search/?type=cloudcast&q={HttpUtility.UrlEncode(query)}").ThrowIfNull();

    public record SearchResults(ImmutableArray<SearchResult> Data, SearchResultsPaging Paging);
    public record SearchResultsPaging(string Next);

    public record SearchResult(SearchResultUser User, string Name, string Url, string Slug, [property: JsonPropertyName("audio_length")] int AudioLength) : IMusicData
    {
        string IMusicData.DirectUrl => Url;
        string IMusicData.Identifier => Slug;

        public SongInfo ToSongInfo() => SongInfo.Create(User.Name, Name, AudioLength);
    }
    public record SearchResultUser(string Name);
}
