using System.Text.Json.Serialization;

namespace InstaPostGenerator.Models;

public class RemoteConfig
{
    [JsonPropertyName("config_url")]
    public string ConfigUrl { get; set; } = "";

    [JsonPropertyName("refresh_interval_hours")]
    public int RefreshIntervalHours { get; set; } = 6;

    [JsonPropertyName("api")]
    public ApiConfig Api { get; set; } = new();

    [JsonPropertyName("settings")]
    public SettingsConfig Settings { get; set; } = new();

    [JsonPropertyName("categories")]
    public Dictionary<string, CategoryConfig> Categories { get; set; } = new();

    [JsonPropertyName("exclude_keywords")]
    public string[] ExcludeKeywords { get; set; } = Array.Empty<string>();

    [JsonPropertyName("rss_feeds")]
    public RssFeedConfig[] RssFeeds { get; set; } = Array.Empty<RssFeedConfig>();

    [JsonPropertyName("image")]
    public ImageConfig Image { get; set; } = new();
}

public class ApiConfig
{
    [JsonPropertyName("serpapi_key")]
    public string SerpApiKey { get; set; } = "";

    [JsonPropertyName("serpapi_url")]
    public string SerpApiUrl { get; set; } = "https://serpapi.com/search";

    [JsonPropertyName("imgbb_key")]
    public string ImgbbKey { get; set; } = "";

    [JsonPropertyName("meta_app_id")]
    public string MetaAppId { get; set; } = "";

    [JsonPropertyName("meta_app_secret")]
    public string MetaAppSecret { get; set; } = "";

    [JsonPropertyName("meta_access_token")]
    public string MetaAccessToken { get; set; } = "";

    [JsonPropertyName("instagram_business_account_id")]
    public string InstagramBusinessAccountId { get; set; } = "";
}

public class SettingsConfig
{
    [JsonPropertyName("language")]
    public string Language { get; set; } = "en";

    [JsonPropertyName("posts_per_run")]
    public int PostsPerRun { get; set; } = 10;

    [JsonPropertyName("max_article_age_hours")]
    public int MaxArticleAgeHours { get; set; } = 24;

    [JsonPropertyName("min_article_score")]
    public int MinArticleScore { get; set; } = 5;

    [JsonPropertyName("story_similarity_threshold")]
    public double StorySimilarityThreshold { get; set; } = 0.5;

    [JsonPropertyName("page_name")]
    public string PageName { get; set; } = "360buzz";
}

public class CategoryConfig
{
    [JsonPropertyName("display_name")]
    public string DisplayName { get; set; } = "";

    [JsonPropertyName("query")]
    public string Query { get; set; } = "";

    [JsonPropertyName("color")]
    public string Color { get; set; } = "#D1FF02";

    [JsonPropertyName("emoji")]
    public string Emoji { get; set; } = "";
}

public class RssFeedConfig
{
    [JsonPropertyName("url")]
    public string Url { get; set; } = "";

    [JsonPropertyName("source")]
    public string Source { get; set; } = "";

    [JsonPropertyName("category")]
    public string Category { get; set; } = "";
}

public class ImageConfig
{
    [JsonPropertyName("width")]
    public int Width { get; set; } = 2160;

    [JsonPropertyName("height")]
    public int Height { get; set; } = 2700;

    [JsonPropertyName("export_width")]
    public int ExportWidth { get; set; } = 1080;

    [JsonPropertyName("export_height")]
    public int ExportHeight { get; set; } = 1350;

    [JsonPropertyName("brand_green")]
    public string BrandGreen { get; set; } = "#D1FF02";

    [JsonPropertyName("brand_yellow")]
    public string BrandYellow { get; set; } = "#FFD700";

    [JsonPropertyName("brand_red")]
    public string BrandRed { get; set; } = "#DC2626";

    [JsonPropertyName("bg_dark")]
    public string BgDark { get; set; } = "#101216";
}
