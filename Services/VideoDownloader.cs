using System.Diagnostics;
using System.Text.Json;

namespace InstaPostGenerator.Services;

public static class VideoDownloader
{
    public static async Task<string> DownloadVideoAsync(string videoUrl, string outputDir)
    {
        try
        {
            Directory.CreateDirectory(outputDir);

            // Try cobalt.tools API (free, no auth needed)
            var result = await DownloadViaCobaltAsync(videoUrl, outputDir);
            if (!string.IsNullOrEmpty(result) && File.Exists(result))
                return result;

            Debug.WriteLine("[VideoDownloader] Download failed");
            return null;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[VideoDownloader] Error: {ex.Message}");
            return null;
        }
    }

    private static async Task<string> DownloadViaCobaltAsync(string videoUrl, string outputDir)
    {
        try
        {
            using var httpClient = new HttpClient();
            httpClient.Timeout = TimeSpan.FromSeconds(120);

            // cobalt.tools API
            var requestBody = new
            {
                url = videoUrl,
                downloadMode = "auto",
                filenameStyle = "basic",
                videoQuality = "720"
            };

            var json = JsonSerializer.Serialize(requestBody);
            var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
            content.Headers.Add("Accept", "application/json");

            Debug.WriteLine($"[VideoDownloader] Calling cobalt API for: {videoUrl}");

            var response = await httpClient.PostAsync("https://api.cobalt.tools/", content);
            var responseJson = await response.Content.ReadAsStringAsync();

            Debug.WriteLine($"[VideoDownloader] Cobalt response: {response.StatusCode} - {responseJson[..Math.Min(200, responseJson.Length)]}");

            if (!response.IsSuccessStatusCode)
                return null;

            var doc = JsonDocument.Parse(responseJson);
            var root = doc.RootElement;

            if (root.TryGetProperty("url", out var downloadUrl))
            {
                var dlUrl = downloadUrl.GetString();
                Debug.WriteLine($"[VideoDownloader] Download URL: {dlUrl?[..Math.Min(80, dlUrl?.Length ?? 0)]}");

                var videoBytes = await httpClient.GetByteArrayAsync(dlUrl);
                var outputPath = Path.Combine(outputDir, $"{Guid.NewGuid():N}.mp4");
                await File.WriteAllBytesAsync(outputPath, videoBytes);

                var fi = new FileInfo(outputPath);
                Debug.WriteLine($"[VideoDownloader] Saved: {fi.Length} bytes");

                if (fi.Length > 10000)
                    return outputPath;
            }

            return null;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[VideoDownloader] Cobalt error: {ex.Message}");
            return null;
        }
    }
}
