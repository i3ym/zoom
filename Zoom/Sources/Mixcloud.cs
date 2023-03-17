using System.Collections.Immutable;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using System.Web;

namespace Zoom.Sources;

public static class Mixcloud
{
    public static async Task<SearchResults> Search(string query) =>
        await Http.Instance.GetFromJsonAsync<SearchResults>($"https://api.mixcloud.com/search/?type=cloudcast&q={HttpUtility.UrlEncode(query)}").ThrowIfNull();


    public record SearchResults(ImmutableArray<SearchResult> Data, SearchResultsPaging Paging);
    public record SearchResultsPaging(string Next);

    public record SearchResult(SearchResultUser User, string Name, string Url, string Slug, [property: JsonPropertyName("audio_length")] int AudioLength)
    {
        public SongInfo ToSongInfo() => new SongInfo(User.Name, Name, AudioLength);
    }
    public record SearchResultUser(string Name);
}
