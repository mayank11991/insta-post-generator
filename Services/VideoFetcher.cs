using System.Text.Json;
using InstaPostGenerator.Models;

namespace InstaPostGenerator.Services;

public static class VideoFetcher
{
    private static readonly HttpClient _http = new();

    public static async Task<List<VideoItem>> FetchYouTubeVideosAsync(string query, int maxResults = 10)
    {
        var apiKey = Config.YOUTUBE_API_KEY;
        if (string.IsNullOrEmpty(apiKey))
        {
            System.Diagnostics.Debug.WriteLine("[VideoFetcher] YouTube API key not configured");
            return new List<VideoItem>();
        }

        try
        {
            var publishedAfter = DateTime.UtcNow.AddDays(-7).ToString("yyyy-MM-ddTHH:mm:ssZ");
            var url = $"https://www.googleapis.com/youtube/v3/search" +
                      $"?part=snippet" +
                      $"&q={Uri.EscapeDataString(query)}" +
                      $"&type=video" +
                      $"&videoDuration=short" +
                      $"&order=relevance" +
                      $"&publishedAfter={publishedAfter}" +
                      $"&maxResults=50" +
                      $"&key={apiKey}";

            var response = await _http.GetStringAsync(url);
            var doc = JsonDocument.Parse(response);

            var allVideos = new List<VideoItem>();
            var seenIds = new HashSet<string>();
            var seenTitles = new List<string>();

            if (doc.RootElement.TryGetProperty("items", out var items))
            {
                foreach (var item in items.EnumerateArray())
                {
                    if (item.TryGetProperty("id", out var id) &&
                        id.TryGetProperty("videoId", out var videoId))
                    {
                        var vid = videoId.GetString() ?? "";
                        if (seenIds.Contains(vid)) continue;
                        seenIds.Add(vid);

                        if (item.TryGetProperty("snippet", out var snippet))
                        {
                            var title = snippet.TryGetProperty("title", out var t) ? t.GetString() ?? "" : "";
                            var description = snippet.TryGetProperty("description", out var d) ? d.GetString() ?? "" : "";
                            var channelTitle = snippet.TryGetProperty("channelTitle", out var c) ? c.GetString() ?? "" : "";
                            var thumbnail = "";

                            if (snippet.TryGetProperty("thumbnails", out var thumbs) &&
                                thumbs.TryGetProperty("high", out var high) &&
                                high.TryGetProperty("url", out var thumbUrl))
                            {
                                thumbnail = thumbUrl.GetString() ?? "";
                            }

                            if (IsRelevantVideo(title, description) && !IsDuplicateTitle(title, seenTitles))
                            {
                                seenTitles.Add(title.ToLowerInvariant());
                                allVideos.Add(new VideoItem
                                {
                                    VideoId = vid,
                                    Title = title,
                                    Description = description,
                                    ChannelName = channelTitle,
                                    ThumbnailUrl = thumbnail,
                                    VideoUrl = $"https://www.youtube.com/watch?v={vid}"
                                });
                            }
                        }
                    }
                }
            }

            var result = allVideos.Take(maxResults).ToList();
            System.Diagnostics.Debug.WriteLine($"[VideoFetcher] Found {result.Count} unique videos for query: {query}");
            return result;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[VideoFetcher] YouTube API error: {ex.Message}");
            return new List<VideoItem>();
        }
    }

    private static bool IsDuplicateTitle(string title, List<string> seenTitles)
    {
        var normalized = title.ToLowerInvariant().Replace(" ", "").Replace("|", "").Replace("-", "").Replace("#", "");
        foreach (var seen in seenTitles)
        {
            var seenNorm = seen.Replace(" ", "").Replace("|", "").Replace("-", "").Replace("#", "");
            if (normalized.Length > 10 && seenNorm.Length > 10)
            {
                var shorter = Math.Min(normalized.Length, seenNorm.Length);
                var compareLen = (int)(shorter * 0.7);
                if (compareLen > 10 && normalized[..compareLen] == seenNorm[..compareLen])
                    return true;
            }
        }
        return false;
    }

    private static bool IsRelevantVideo(string title, string description)
    {
        var combined = (title + " " + description).ToLower();

        // Exclude unwanted content
        var excludePatterns = new[]
        {
            "compilation", "best of", "top 10", "funny moments",
            "live stream", "full episode", "full movie",
            "behind the scenes", "interview", "press conference",
            "podcast", "reaction", "review"
        };

        foreach (var pattern in excludePatterns)
        {
            if (combined.Contains(pattern))
                return false;
        }

        // Must have some relevance
        return true;
    }
}

public class VideoItem
{
    public string VideoId { get; set; } = "";
    public string Title { get; set; } = "";
    public string Description { get; set; } = "";
    public string ChannelName { get; set; } = "";
    public string ThumbnailUrl { get; set; } = "";
    public string VideoUrl { get; set; } = "";
    public string LocalFilePath { get; set; } = "";
    public string Caption { get; set; } = "";
    public string Hashtags { get; set; } = "";
}