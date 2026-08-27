using System.Text.Json;
using InstaPostGenerator.Models;

namespace InstaPostGenerator.Services;

public static class RemoteConfigService
{
    private static RemoteConfig? _cached;
    private static DateTime _lastFetch = DateTime.MinValue;
    private static readonly HttpClient _http = new();
    private static readonly string _cacheDir;
    private static readonly string _cacheFile;

    // SET THIS to your GitHub raw config URL
    public static string ConfigUrl { get; set; } = "";

    static RemoteConfigService()
    {
#if ANDROID
        _cacheDir = Android.App.Application.Context.FilesDir?.AbsolutePath ?? "/tmp";
#else
        _cacheDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".instapost");
#endif
        _cacheFile = Path.Combine(_cacheDir, "config_cache.json");
        Directory.CreateDirectory(_cacheDir);
    }

    public static async Task<RemoteConfig> GetConfigAsync()
    {
        if (_cached != null && (DateTime.UtcNow - _lastFetch).TotalHours < 6)
            return _cached;

        // Try fetching from remote
        if (!string.IsNullOrEmpty(ConfigUrl))
        {
            try
            {
                var json = await _http.GetStringAsync(ConfigUrl);
                var config = JsonSerializer.Deserialize<RemoteConfig>(json);
                if (config != null)
                {
                    _cached = config;
                    _lastFetch = DateTime.UtcNow;
                    // Save cache
                    await File.WriteAllTextAsync(_cacheFile, json);
                    return _cached;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Config] Remote fetch failed: {ex.Message}");
            }
        }

        // Try loading from cache
        if (File.Exists(_cacheFile))
        {
            try
            {
                var json = await File.ReadAllTextAsync(_cacheFile);
                var config = JsonSerializer.Deserialize<RemoteConfig>(json);
                if (config != null)
                {
                    _cached = config;
                    return _cached;
                }
            }
            catch { }
        }

        // Return default config
        _cached = new RemoteConfig();
        return _cached;
    }

    public static RemoteConfig GetConfig()
    {
        if (_cached != null && (DateTime.UtcNow - _lastFetch).TotalHours < 6)
            return _cached;

        // Try loading from cache synchronously
        if (File.Exists(_cacheFile))
        {
            try
            {
                var json = File.ReadAllText(_cacheFile);
                var config = JsonSerializer.Deserialize<RemoteConfig>(json);
                if (config != null)
                {
                    _cached = config;
                    _lastFetch = DateTime.UtcNow;
                    return _cached;
                }
            }
            catch { }
        }

        _cached = new RemoteConfig();
        return _cached;
    }

    public static void InvalidateCache()
    {
        _cached = null;
        _lastFetch = DateTime.MinValue;
    }
}
