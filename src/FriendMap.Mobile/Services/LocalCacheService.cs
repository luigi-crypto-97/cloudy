using System.Text.Json;

namespace FriendMap.Mobile.Services;

public static class LocalCacheService
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    private static string GetPath(string key) => Path.Combine(FileSystem.CacheDirectory, $"friendmap_cache_{key}.json");

    public static void Set<T>(string key, T value, TimeSpan? ttl = null)
    {
        try
        {
            var path = GetPath(key);
            var wrapper = new CacheWrapper<T>
            {
                Data = value,
                ExpiresAtUtc = ttl.HasValue ? DateTimeOffset.UtcNow.Add(ttl.Value) : DateTimeOffset.MaxValue
            };
            var json = JsonSerializer.Serialize(wrapper, JsonOptions);
            File.WriteAllText(path, json);
        }
        catch { /* ignore */ }
    }

    public static T? Get<T>(string key) where T : class
    {
        try
        {
            var path = GetPath(key);
            if (!File.Exists(path)) return null;

            var json = File.ReadAllText(path);
            var wrapper = JsonSerializer.Deserialize<CacheWrapper<T>>(json, JsonOptions);
            if (wrapper is null || wrapper.ExpiresAtUtc < DateTimeOffset.UtcNow)
            {
                File.Delete(path);
                return null;
            }
            return wrapper.Data;
        }
        catch { return null; }
    }

    public static void Remove(string key)
    {
        try
        {
            var path = GetPath(key);
            if (File.Exists(path)) File.Delete(path);
        }
        catch { /* ignore */ }
    }

    public static void ClearAll()
    {
        try
        {
            foreach (var file in Directory.GetFiles(FileSystem.CacheDirectory, "friendmap_cache_*.json"))
                File.Delete(file);
        }
        catch { /* ignore */ }
    }

    private class CacheWrapper<T>
    {
        public T? Data { get; set; }
        public DateTimeOffset ExpiresAtUtc { get; set; }
    }
}
