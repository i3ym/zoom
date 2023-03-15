namespace Zoom;

public static class FileCache
{
    static readonly string CacheDirectory = ZoomConfig.Instance.Get<string>("cachepath");
    static readonly string InfoJsonPath = Path.Combine(CacheDirectory, "info.json");

    static readonly Dictionary<string, string> CachedFiles;

    static FileCache()
    {
        if (File.Exists(InfoJsonPath))
            CachedFiles = JsonConvert.DeserializeObject<Dictionary<string, string>>(File.ReadAllText(InfoJsonPath)).ThrowIfNull();
        else CachedFiles = new();

        Directory.CreateDirectory(CacheDirectory);
    }

    static void Save() => File.WriteAllText(InfoJsonPath, JsonConvert.SerializeObject(CachedFiles, Formatting.Indented));

    public static void AddCached(string key, string path)
    {
        CachedFiles[key] = Path.GetFileName(path);
        Save();
    }

    public static string? TryGetCached(string key)
    {
        if (CachedFiles.TryGetValue(key, out var path))
        {
            if (File.Exists(path)) return path;
            CachedFiles.Remove(key);
        }

        return null;
    }

    public static string GetNewPath(string key) => CacheDirectory + Guid.NewGuid().ToString();
}