namespace Zoom.Sources;

public class YandexRadio : Radio
{
    public override string Name => "Яндекс Радио (" + Station + ")";
    readonly IAsyncEnumerator<Song> SongsEnumerable;
    readonly string Station;

    public YandexRadio(string station)
    {
        Station = station;
        SongsEnumerable = CreateInfiniteYandexRadio().GetAsyncEnumerator();
    }


    async IAsyncEnumerable<Song> CreateInfiniteYandexRadio()
    {
        string previous = string.Empty;

        while (true)
        {
            var (batchId, tracks) = await YandexMusic.FetchStationSongs(Station, previous);
            foreach (var song in tracks)
            {
                yield return song.Song;
                previous += "%2C" + song.Id;

                _ = Task.Run(() => YandexMusic.SendEndPlayingTrack(Station, int.Parse(song.Id), batchId));
            }

            if (previous.Length > 1_000) previous = string.Empty;
        }
    }
    protected override async Task<Song> LoadNext()
    {
        await SongsEnumerable.MoveNextAsync();
        return SongsEnumerable.Current;
    }
}