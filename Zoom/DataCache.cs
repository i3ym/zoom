using System.Diagnostics.CodeAnalysis;

namespace Zoom;

public static class DataCache
{
    public static readonly string CacheDirectory = ZoomConfig.Instance.Get<string>("cachepath");
    static readonly string JsonPath = Path.Combine(CacheDirectory, "cache.json");
    static readonly JsonConfig Cache = JsonConfig.LoadOrCreate(JsonPath);


    [return: NotNullIfNotNull(nameof(defaultval))]
    public static T? Get<T>(string category, string sub, string name, T? defaultval) => Cache.Object(category).Object(sub).Get<T>(name, defaultval);
    public static void Set<T>(string category, string sub, string name, T value) => Cache.Object(category).Object(sub).Set(name, value);


    public static string? GetSearch(string category, string query) => Get(category, "search", query, null as string);
    public static void SetSearch(string category, string query, string value) => Set(category, "search", query, value);

    static JsonConfig GetData(string category, string id) => Cache.Object(category).Object("data").Object(id);

    public static SongInfo? GetSongInfo(string category, string id) => GetData(category, id).Get("info", null as SongInfo);
    public static void SetSongInfo(string category, string id, SongInfo value) => GetData(category, id).Set("info", value);

    public static string? GetSongCachedUrl(string category, string id) => GetData(category, id).Get("url", null as string);
    public static void SetSongCachedUrl(string category, string id, string value) => GetData(category, id).Set("url", value);

    public static string? GetSongCacheFile(string category, string id) => GetData(category, id).Get("cachefile", null as string);
    public static void SetSongCacheFile(string category, string id, string value) => GetData(category, id).Set("cachefile", value);


    public static Song? TryGetCachedSearch(string category, string query)
    {
        var id = GetSearch(category, query);
        if (id is null) return null;

        var data = GetSongInfo(category, id);
        if (data is null) return null;

        var cachefile = GetSongCacheFile(category, id);
        if (cachefile is null) return null;

        return new Song(data, DirectSoundStream.FromPath(Path.Combine(CacheDirectory, cachefile)));
    }
    public static string? TryGetCachedUrl(string category, string query)
    {
        var id = GetSearch(category, query);
        if (id is null) return null;

        return GetSongCachedUrl(category, id);
    }
}

/*
{
    mixcloud: {
        search: {
            "otographic arts 159": "kenji-sekiguchi-nhato-otographic-arts-159-2023-03-07",
        },
        data: {
            "kenji-sekiguchi-nhato-otographic-arts-159-2023-03-07": {
                cachefile: "bfebcd37-cc9b-40ce-ab25-a1027162e096",
                url: "https://audio4.mixcloud.com/secure/dash_multi/,f/9/e/8/bb76-eab0-4db5-b912-e92d2ff06f8c-64K,f/9/e/8/bb76-eab0-4db5-b912-e92d2ff06f8c-192K,.m4a.urlset/manifest.mpd",
                info: {
                    author: "Otographic Music",
                    title: "Kenji Sekiguchi & Nhato - Otographic Arts 159 2023-03-07",
                    lengthseconds: 7171,
                },
            },
        },
    },
}
*/