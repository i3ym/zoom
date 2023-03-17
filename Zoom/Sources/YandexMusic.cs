using System.Collections.Immutable;
using System.Net;

namespace Zoom.Sources;

public static class YandexMusic
{
    static readonly Logger Logger = LogManager.GetCurrentClassLogger();

    static ImmutableDictionary<string, string>? Stations;
    static string? Token;


    public static async Task<ImmutableDictionary<string, string>> GetStations() => Stations ??= (await LoadStations()).ToImmutableDictionary(x => x.name.ToUpperInvariant(), x => x.url);
    static async Task<(string url, string name)[]> LoadStations()
    {
        var data = await JDownload("https://api.music.yandex.net/rotor/stations/list");
        return data["result"]!.AsJEnumerable().Select(x => (x["station"]!["id"]!["type"]!.Value<string>() + ":" + x["station"]!["id"]!["tag"]!.Value<string>(), x["station"]!["name"]!.Value<string>())).ToArray();
    }

    static async Task<YandexTrack> GetTrackInfo(JToken json)
    {
        if (json["track"] is { } t) json = t;

        var id = json["id"]?.Value<string>() ?? throw new NullReferenceException();
        var title = json["title"]?.Value<string>() ?? throw new NullReferenceException();
        var artists = json?["artists"]?.Select(x => x?["name"]?.Value<string>()) ?? throw new NullReferenceException();
        var durationMs = json?["durationMs"]?.Value<int>() ?? throw new NullReferenceException();

        var info = new SongInfo(string.Join(" & ", artists), title, durationMs / 1000);
        var url = await FetchSongUrl(id);

        return new YandexTrack(id, new Song(info, MemorySoundStream.FromUrl(url)));
    }

    public static async Task<string> FetchSongUrl(string id)
    {
        var jinfo = await JDownload("https://api.music.yandex.net/tracks/" + id + "/download-info");
        var infoUrl = jinfo["result"]?.FirstOrDefault()?["downloadInfoUrl"]?.Value<string>() ?? throw new NullReferenceException();

        var info2 = await JDownload(infoUrl + "&format=json", new { host = "", path = "", ts = "" });
        return "https://" + info2.host + "/get-mp3/efa0d0bcf068ed22f8a8a61ebd1ea9e4/" + info2.ts + info2.path + "?track-id=" + id;
    }
    public static async Task<YandexTrack[]> FetchAlbumSongs(string albumid)
    {
        var jinfo = await JDownload("https://music.yandex.ru/handlers/album.jsx?album=" + albumid + "&lang=ru&external-domain=music.yandex.ru&overembed=false");
        var volumes = jinfo["volumes"]?.FirstOrDefault()?.AsJEnumerable().ToArray();
        if (volumes is null) return Array.Empty<YandexTrack>();

        return await Task.WhenAll(volumes.Select(GetTrackInfo));
    }
    public static async Task<YandexTrack[]> FetchSongs(params (string id, string albumid)[] tracks)
    {
        var trackstr = string.Join("%2C", tracks.Select(x => x.id));
        var json = await JDownload("https://music.yandex.ru/api/v2.1/handlers/tracks?tracks=" + trackstr + "&external-domain=music.yandex.ru&overembed=no&__t=" + DateTimeOffset.Now.ToUnixTimeMilliseconds());

        return await Task.WhenAll(json.AsJEnumerable().Select(x => x["track"]!).Select(GetTrackInfo));
    }


    public static async Task<(string batchId, YandexTrack[] tracks)> FetchStationSongs(string station, string previous)
    {
        var json = await JDownload("https://api.music.yandex.net/rotor/station/" + station + "/tracks?settings2=true&queue=" + previous);
        return (json["result"]!["batchId"]!.Value<string>(), await Task.WhenAll(json["result"]!["sequence"]!.AsJEnumerable().Select(GetTrackInfo)));
    }
    public static Task SendEndPlayingTrack(string station, int trackId, string batchId)
    {
        return RadioFeedback("trackFinished", station, trackId, batchId);

        /*return self.rotor_station_feedback(
            station,
            'trackFinished',
            timestamp,
            track_id=track_id,
            total_played_seconds=total_played_seconds,
            batch_id=batch_id,
            timeout=timeout,
            *args,
            **kwargs,
        )*/

        /*
        if timestamp is None:
            timestamp = datetime.now().timestamp()

        url = f'{self.base_url}/rotor/station/{station}/feedback'

        params = {}
        data = {'type': type_, 'timestamp': timestamp}

        if batch_id:
            params = {'batch-id': batch_id}

        if track_id:
            data.update({'trackId': track_id})

        if from_:
            data.update({'from': from_})

        if total_played_seconds:
            data.update({'totalPlayedSeconds': total_played_seconds})

        result = self._request.post(url, params=params, json=data, timeout=timeout, *args, **kwargs)

        return result == 'ok'*/
    }
    static Task RadioFeedback(string type, string station, int trackId, string batchId) =>
        PostJ("https://api.music.yandex.net/rotor/station/" + station + "/feedback?batch-id=" + batchId,
            new
            {
                type = type,
                timestamp = DateTimeOffset.Now.ToUnixTimeMilliseconds(),
                trackId = trackId,
                from = 0,
                totalPlayedSeconds = 0,
            });

    public static Task<string> Download(string url, params string[] headers) => Fetch(url, headers, _ => Task.CompletedTask);
    static Task<string> Post(string url, params (string key, string value)[] post) =>
        Fetch(url, Array.Empty<string>(),
            async request =>
            {
                request.Method = "POST";

                var data = await new FormUrlEncodedContent(post.Select(x => KeyValuePair.Create(x.key, x.value))!).ReadAsByteArrayAsync();

                request.ContentType = "application/x-www-form-urlencoded; charset=UTF-8";
                request.ContentLength = data.Length;
                await request.GetRequestStream().WriteAsync(data);
            });
    static Task<string> PostJ<T>(string url, T obj) =>
        Fetch(url, Array.Empty<string>(),
            async request =>
            {
                request.Method = "POST";

                var data = await new StringContent(JsonConvert.SerializeObject(obj)).ReadAsByteArrayAsync();

                request.ContentType = "application/json; charset=UTF-8";
                request.ContentLength = data.Length;
                await request.GetRequestStream().WriteAsync(data);
            });
    static async Task<string> Fetch(string url, string[] headers, Func<HttpWebRequest, Task> modify)
    {
        if (!Environment.StackTrace.Contains("Auth"))
            Token ??= await Auth();

        var request = WebRequest.CreateHttp(url);
        if (Token is { })
            request.Headers.Add("Authorization", "OAuth " + Token);

        foreach (var header in headers)
            request.Headers.Add(header);

        await modify(request);

        try
        {
            WebResponse response = await request.GetResponseAsync();
            using var stream = response.GetResponseStream();
            using var reader = new StreamReader(stream);

            var output = await reader.ReadToEndAsync();
            return output;
        }
        catch (WebException ex)
        {
            Logger.Error(ex);
            if (ex.Response is null) throw;

            using var stream = ex.Response.GetResponseStream();
            using var reader = new StreamReader(stream);

            var output = await reader.ReadToEndAsync();
            return output;
        }
    }

    public static async Task<JToken> JDownload(string url, params string[] headers) => JToken.Parse(await Download(url, headers));
    public static async Task<T> JDownload<T>(string url, T anonymousType, params string[] headers) => JsonConvert.DeserializeAnonymousType(await Download(url, headers), anonymousType);
    public static async Task<JToken> JPost(string url, params (string key, string value)[] post) => JToken.Parse(await Post(url, post));
    public static async Task<T> JPost<T>(string url, T anonymousType, params (string key, string value)[] post) => JsonConvert.DeserializeAnonymousType(await Post(url, post), anonymousType);

    public static async Task<YandexTrack[]> ParseYandexMusicUrlAsync(string url)
    {
        const string track = "track/";
        const string album = "album/";


        string? trackId = null, albumId = null;

        var idx = url.IndexOf(album);
        if (idx != -1)
        {
            var slashidx = url.IndexOf('/', idx + album.Length);
            if (slashidx == -1) slashidx = url.Length;

            albumId = url.Substring(idx + album.Length, slashidx - idx - album.Length);
        }

        idx = url.IndexOf(track);
        if (idx != -1)
        {
            var slashidx = url.IndexOf('/', idx + track.Length);
            if (slashidx == -1) slashidx = url.Length - idx - track.Length;

            trackId = url.Substring(idx + track.Length, slashidx);

            if (url[idx] == '#') albumId = url.Substring(slashidx + 1);
        }

        if (albumId is null) return Array.Empty<YandexTrack>();
        if (trackId is null) return YandexMusic.FetchAlbumSongs(albumId).Result;
        return await YandexMusic.FetchSongs((trackId, albumId));
    }



    static Task<string> Auth()
    {
        var login = ZoomConfig.Instance.Object("yandex").Get<string>("login");
        var password = ZoomConfig.Instance.Object("yandex").Get<string>("password");

        return Auth(login, password);
    }
    static async Task<string> Auth(string login, string password)
    {
        var yatoken = ZoomConfig.Instance.Get<string>("yatoken", null);
        if (yatoken is not null) return yatoken;

        string? captchaKey = null, captchaAnswer = null;
        JToken token = null!;
        for (int i = 0; i < 2; i++)
        {
            token = await post();
            if (token["error"] is { })
            {
                if (token["error_description"]?.Value<string>()?.Contains("CAPTCHA") ?? false)
                {
                    Logger.Info("CAPTCHA needed, " + token["x_captcha_url"]?.Value<string>());

                    captchaKey = token["x_captcha_key"]?.Value<string>();
                    captchaAnswer = Console.ReadLine();
                }
                else
                {
                    Logger.Error(token.ToString());
                    throw new InvalidOperationException();
                }
            }
        }

        var tk = token["access_token"]?.Value<string>() ?? throw new InvalidOperationException();
        ZoomConfig.Instance.Object("cache").Set("yatoken", tk);

        return tk;


        Task<JToken> post()
        {
            var post = new[]
            {
                ("grant_type", "password"),
                ("client_id", "23cabbbdc6cd418abb4b39c32c41195d"),
                ("client_secret", "53bc75238f0c4d08a118e51fe9203300"),
                ("username", login),
                ("password", password),
            };

            if (captchaKey is { } && captchaAnswer is { })
                post = post.Append(("x_captcha_key", captchaKey)).Append(("x_captcha_answer", captchaAnswer)).ToArray();

            return JPost("https://oauth.yandex.com/token", post);
        }
    }


    public record YandexTrack(string Id, Song Song);
}