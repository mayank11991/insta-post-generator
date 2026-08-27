using System;
using System.Globalization;
using System.Collections.Generic;
using System.IO;
using SkiaSharp;
using InstaPostGenerator.Services;

namespace InstaPostGenerator.Models;

public static class Config
{
    private static RemoteConfig? _remote;

    private static RemoteConfig Remote => _remote ??= RemoteConfigService.GetConfig();

    // API Configuration - falls back to hardcoded if remote not loaded
    public static string SERPAPI_KEY => Remote.Api.SerpApiKey;
    public static string API_URL => Remote.Api.SerpApiUrl;
    public static string META_APP_ID => Remote.Api.MetaAppId;
    public static string META_APP_SECRET => Remote.Api.MetaAppSecret;
    public static string META_ACCESS_TOKEN => Remote.Api.MetaAccessToken;
    public static string INSTAGRAM_BUSINESS_ACCOUNT_ID => Remote.Api.InstagramBusinessAccountId;
    public static string IMGBB_API_KEY => Remote.Api.ImgbbKey;

    // Settings
    public static string LANGUAGE => Remote.Settings.Language;
    public static int POSTS_PER_RUN => Remote.Settings.PostsPerRun;
    public static int MAX_ARTICLE_AGE_HOURS => Remote.Settings.MaxArticleAgeHours;
    public static int MIN_ARTICLE_SCORE => Remote.Settings.MinArticleScore;
    public static double STORY_SIMILARITY_THRESHOLD => Remote.Settings.StorySimilarityThreshold;
    public static string PAGE_NAME => Remote.Settings.PageName;

    // Image Configuration
    public static int IMAGE_WIDTH => Remote.Image.Width;
    public static int IMAGE_HEIGHT => Remote.Image.Height;
    public static int EXPORT_WIDTH => Remote.Image.ExportWidth;
    public static int EXPORT_HEIGHT => Remote.Image.ExportHeight;

    // Colors (parsed from remote hex strings)
    public static SKColor BRAND_GREEN => ParseColor(Remote.Image.BrandGreen, new SKColor(209, 255, 2));
    public static SKColor BRAND_YELLOW => ParseColor(Remote.Image.BrandYellow, new SKColor(255, 215, 0));
    public static SKColor BRAND_RED => ParseColor(Remote.Image.BrandRed, new SKColor(220, 38, 38));
    public static SKColor WHITE => new SKColor(255, 255, 255);
    public static SKColor BLACK => new SKColor(0, 0, 0);
    public static SKColor DARK_GRAY => new SKColor(30, 30, 30);
    public static SKColor MEDIUM_GRAY => new SKColor(60, 60, 60);
    public static SKColor LIGHT_GRAY => new SKColor(180, 180, 180);
    public static SKColor BG_DARK => ParseColor(Remote.Image.BgDark, new SKColor(16, 18, 22));
    public static SKColor GRADIENT_START => new SKColor(20, 30, 48);
    public static SKColor GRADIENT_END => new SKColor(110, 64, 148);

    // Topics - built from remote categories
    public static string[] TOPICS => Remote.Categories.Keys.ToArray();

    public static Dictionary<string, string> TOPIC_QUERIES
    {
        get
        {
            var dict = new Dictionary<string, string>();
            foreach (var kv in Remote.Categories)
                dict[kv.Key] = kv.Value.Query;
            return dict;
        }
    }

    // Exclusion keywords
    public static string[] EXCLUDE_KEYWORDS => Remote.ExcludeKeywords;

    // RSS Feeds
    public static (string Url, string SourceName, string Category)[] RSS_FEEDS
    {
        get
        {
            return Remote.RssFeeds.Select(f => (f.Url, f.Source, f.Category)).ToArray();
        }
    }

    // Category display info
    public static string GetCategoryDisplayName(string key)
    {
        if (Remote.Categories.TryGetValue(key, out var cat))
            return cat.DisplayName;
        return key;
    }

    public static string GetCategoryEmoji(string key)
    {
        if (Remote.Categories.TryGetValue(key, out var cat))
            return cat.Emoji;
        return "";
    }

    public static string GetCategoryColor(string key)
    {
        if (Remote.Categories.TryGetValue(key, out var cat))
            return cat.Color;
        return "#D1FF02";
    }

    // Output paths
    public static string GetOutputDir()
    {
#if ANDROID
        try
        {
            var downloadsDir = Android.OS.Environment.GetExternalStoragePublicDirectory(Android.OS.Environment.DirectoryDownloads);
            if (downloadsDir != null)
            {
                var dir = Path.Combine(downloadsDir.AbsolutePath, "insta-post");
                Directory.CreateDirectory(dir);
                var testFile = Path.Combine(dir, ".write_test");
                File.WriteAllText(testFile, "test");
                File.Delete(testFile);
                return dir;
            }
        }
        catch { }

        var fallback = Android.App.Application.Context.GetExternalFilesDir(null);
        var fallbackDir = Path.Combine(fallback?.AbsolutePath ?? "/sdcard/Download", "insta-post");
        Directory.CreateDirectory(fallbackDir);
        return fallbackDir;
#else
        return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "insta-post");
#endif
    }

    public static string GetSeenFile()
    {
        return Path.Combine(GetOutputDir(), "seen.json");
    }

    // Font paths
    public const string FONT_BRICOLAGE = "BricolageGrotesque.ttf";
    public const string FONT_HEADING = "Rougan.otf";
    public const string FONT_SUBHEADING = "Montserrat-Italic.ttf";
    public const string FONT_HINDI = "NotoSansDevanagari-Regular.ttf";

    // Helper to parse hex color
    private static SKColor ParseColor(string hex, SKColor fallback)
    {
        try
        {
            hex = hex.TrimStart('#');
            if (hex.Length == 6)
                return SKColor.Parse("#" + hex);
            else if (hex.Length == 8)
                return SKColor.Parse("#" + hex);
        }
        catch { }
        return fallback;
    }

    // Force refresh config from remote
    public static async Task RefreshConfigAsync()
    {
        RemoteConfigService.InvalidateCache();
        _remote = await RemoteConfigService.GetConfigAsync();
    }
}
