namespace Zoom;

public static class DataCache
{
    public static readonly string CacheDirectory = ZoomConfig.Instance.Get<string>("cachepath");
    static readonly string JsonPath = Path.Combine(CacheDirectory, "cache.json");
    static readonly JsonConfig Cache = JsonConfig.LoadOrCreate(JsonPath);


    static JsonConfig GetData(string category, string id) => Cache.Object(category).Object("data").Object(id);

    public static SongInfo? GetSongInfo(string category, string id) => GetData(category, id).Get("info", null as SongInfo);
    public static void SetSongInfo(string category, string id, SongInfo value) => GetData(category, id).Set("info", value);

    public static string? GetSongCacheFile(string category, string id) => GetData(category, id).Get("cachefile", null as string);
    public static void SetSongCacheFile(string category, string id, string value) => GetData(category, id).Set("cachefile", value);


    public static Song? TryGetCachedSong(string category, string data)
    {
        var info = GetSongInfo(category, data);
        if (info is null) return null;

        var cachefile = GetSongCacheFile(category, data);
        if (cachefile is null) return null;

        return new Song(info, DirectSoundStream.FromFile(Path.Combine(CacheDirectory, cachefile)));
    }
}

/*
{
  youtube: {
    data: {
      "dQw4w9WgXcQ": {
        info: {
          Title: "Rick Astley - Never Gonna Give You Up (Official Music Video)",
          LengthSeconds: 213,
        },
        cachefile: "be976435-4ae6-466d-b333-07d25e3d7e38",
      },
    },
  },
}
*/