using System.Diagnostics;
using System.Text.Json;

namespace InstaPostGenerator.Services;

public static class VideoDownloader
{
    private static readonly string[] LogFile = { "/sdcard/Download/insta-post/dl_debug.log" };

    private static void Log(string msg)
    {
        var line = $"{DateTime.Now:HH:mm:ss} {msg}";
        Debug.WriteLine($"[VideoDownloader] {msg}");
        try { File.AppendAllText(LogFile[0], line + "\n"); } catch { }
    }

    public static async Task<string> DownloadVideoAsync(string videoUrl, string outputDir)
    {
        try
        {
            Directory.CreateDirectory(outputDir);
            File.WriteAllText(LogFile[0], "");

            // Try cobalt v7 API
            var result = await DownloadViaCobaltAsync(videoUrl, outputDir);
            if (!string.IsNullOrEmpty(result) && File.Exists(result))
                return result;

            Log("All methods failed");
            return null;
        }
        catch (Exception ex)
        {
            Log($"Error: {ex.Message}");
            return null;
        }
    }

    private static async Task<string> DownloadViaCobaltAsync(string videoUrl, string outputDir)
    {
        try
        {
            using var httpClient = new HttpClient();
            httpClient.Timeout = TimeSpan.FromSeconds(120);
            httpClient.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Linux; Android 14) AppleWebKit/537.36");
            httpClient.DefaultRequestHeaders.Add("Accept", "application/json");

            // Try cobalt v7 API (no auth needed)
            var requestBody = new Dictionary<string, object>
            {
                ["url"] = videoUrl,
                ["vCodec"] = "h264",
                ["vQuality"] = "720",
                ["aFormat"] = "mp3",
                ["isAudioOnly"] = false,
                ["isNoTTWatermark"] = true
            };

            var json = JsonSerializer.Serialize(requestBody);
            var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");

            Log($"Calling cobalt API: {videoUrl}");

            var response = await httpClient.PostAsync("https://co.wuk.sh/api/json", content);
            var responseJson = await response.Content.ReadAsStringAsync();

            Log($"Cobalt {response.StatusCode}: {responseJson[..Math.Min(300, responseJson.Length)]}");

            if (!response.IsSuccessStatusCode)
                return null;

            var doc = JsonDocument.Parse(responseJson);
            var root = doc.RootElement;

            if (root.TryGetProperty("url", out var dlUrlProp))
            {
                var dlUrl = dlUrlProp.GetString();
                Log($"Download URL: {dlUrl?[..Math.Min(80, dlUrl?.Length ?? 0)]}");

                var videoBytes = await httpClient.GetByteArrayAsync(dlUrl);
                var outputPath = Path.Combine(outputDir, $"{Guid.NewGuid():N}.mp4");
                await File.WriteAllBytesAsync(outputPath, videoBytes);

                var fi = new FileInfo(outputPath);
                Log($"Saved: {fi.Length} bytes -> {outputPath}");

                if (fi.Length > 10000)
                    return outputPath;
            }

            if (root.TryGetProperty("error", out var errProp))
            {
                Log($"API error: {errProp.GetString()}");
            }

            return null;
        }
        catch (Exception ex)
        {
            Log($"Cobalt error: {ex.Message}");
            return null;
        }
    }
}
