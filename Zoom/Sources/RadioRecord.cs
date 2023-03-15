using System.Collections.Immutable;

namespace Zoom.Sources;

public static class RadioRecord
{
    static ImmutableDictionary<int, Station>? Stations;

    public static async Task<ImmutableDictionary<int, Station>> GetStations() => Stations ??= await LoadStations();
    static async Task<ImmutableDictionary<int, Station>> LoadStations()
    {
        var sstations = await new HttpClient().GetStringAsync("https://www.radiorecord.ru/api/stations/");
        var jstations = JObject.Parse(sstations);

        return jstations["result"]?["stations"]?.AsJEnumerable().Select(x => x.ToObject<Station>() ?? throw new InvalidOperationException()).ToImmutableDictionary(x => x.Id, x => x) ?? throw new InvalidOperationException();
    }


    public record Station(string Title, int Id, string Prefix, [JsonProperty("stream_64")] string Stream64, [JsonProperty("stream_128")] string Stream128);
}