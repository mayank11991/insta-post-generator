using System.Net.Http.Headers;
using System.Text.RegularExpressions;
using System.Xml;
using HtmlAgilityPack;
using InstaPostGenerator.Models;
using Newtonsoft.Json.Linq;
using MauiSourceInfo = Microsoft.Maui.SourceInfo;

namespace InstaPostGenerator.Services;

public static class NewsFetcher
{
    private static readonly Lazy<HttpClient> _httpClientLazy = new(() =>
    {
        var client = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
        client.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/126.0.0.0 Safari/537.36");
        client.DefaultRequestHeaders.AcceptLanguage.Add(new StringWithQualityHeaderValue("en-US"));
        client.DefaultRequestHeaders.AcceptLanguage.Add(new StringWithQualityHeaderValue("en", 0.9));
        return client;
    });
    private static HttpClient _httpClient => _httpClientLazy.Value;

    public static string LastFetchDebug { get; private set; } = "";

    public static async Task<List<Article>> FetchResultsAsync(string topic, int maxPages = 5, Func<Task<List<Article>>> fetchMore = null)
    {
        var allResults = new List<Article>();
        var debug = new System.Text.StringBuilder();
        debug.AppendLine($"=== FETCH DEBUG: {topic} ===");

        // RSS scraper
        try
        {
            debug.AppendLine("Fetching RSS feeds...");
            var rssArticles = await FetchTopicAsync(topic);
            debug.AppendLine($"RSS: got {rssArticles.Count} articles");
            allResults.AddRange(rssArticles);
        }
        catch (Exception ex)
        {
            var msg = ex.InnerException?.Message ?? ex.Message;
            debug.AppendLine($"RSS FAILED: {msg}");
            Console.WriteLine($"RSS scraper error: {msg}");
        }

        // SerpAPI Google News
        var query = Config.TOPIC_QUERIES.GetValueOrDefault(topic, topic);
        debug.AppendLine($"SerpAPI query: {query}");
        for (int page = 0; page < maxPages; page++)
        {
            try
            {
                debug.AppendLine($"SerpAPI page {page}...");
                var results = await FetchPageAsync(query, page);
                debug.AppendLine($"SerpAPI page {page}: got {results.Count} articles");
                if (!results.Any()) break;
                allResults.AddRange(results);
            }
            catch (Exception ex)
            {
                var msg = ex.InnerException?.Message ?? ex.Message;
                debug.AppendLine($"SerpAPI page {page} FAILED: {msg}");
                Console.WriteLine($"SerpAPI error: {msg}");
                break;
            }
        }

        debug.AppendLine($"Before filters: {allResults.Count} total");

        // Filter South Indian content
        var before = allResults.Count;
        allResults = allResults.Where(a => !IsExcluded(a)).ToList();
        var excluded = before - allResults.Count;
        if (excluded > 0)
            debug.AppendLine($"South Indian filter: removed {excluded}");

        // Filter to last 24 hours
        allResults = FilterRecentArticles(allResults);
        debug.AppendLine($"After date filter: {allResults.Count}");

        LastFetchDebug = debug.ToString();
        Console.WriteLine($"Total results fetched: {allResults.Count}");
        return allResults;
    }

    private static async Task<List<Article>> FetchPageAsync(string query, int page)
    {
        var parameters = new Dictionary<string, string>
        {
            { "engine", "google_news" },
            { "q", query },
            { "api_key", Config.SERPAPI_KEY }
        };

        if (page > 0)
            parameters["start"] = (page * 10).ToString();

        System.Diagnostics.Debug.WriteLine($"Fetching topic='{query}' page={page}");

        var queryString = string.Join("&", parameters.Select(p => $"{Uri.EscapeDataString(p.Key)}={Uri.EscapeDataString(p.Value)}"));
        var url = $"{Config.API_URL}?{queryString}";

        var response = await _httpClient.GetAsync(url);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync();
        var data = JObject.Parse(json);
        var newsResults = data["news_results"] as JArray;

        var results = new List<Article>();
        if (newsResults != null)
        {
            foreach (var item in newsResults)
            {
                var article = new Article
                {
                    Title = item["title"]?.ToString() ?? "",
                    Link = item["link"]?.ToString() ?? "",
                    Thumbnail = item["thumbnail"]?.ToString() ?? "",
                    Source = new InstaPostGenerator.Models.SourceInfo { Name = item["source"]?.ToString() ?? "" },
                    Summary = item["snippet"]?.ToString() ?? "",
                    IsoDate = item["date"]?.ToString() ?? "",
                    Category = ""
                };
                if (!string.IsNullOrEmpty(article.Title) && !string.IsNullOrEmpty(article.Link))
                    results.Add(article);
            }
        }

        return results;
    }

    private static bool IsExcluded(Article article)
    {
        var title = (article.Title ?? "").ToLowerInvariant();
        var summary = (article.Summary ?? "").ToLowerInvariant();
        var text = $"{title} {summary}";

        return Config.EXCLUDE_KEYWORDS.Any(kw => text.Contains(kw.ToLowerInvariant()));
    }

    private static DateTime? ParseDate(string dateStr)
    {
        if (string.IsNullOrWhiteSpace(dateStr))
            return null;

        dateStr = dateStr.Trim();

        // Relative dates
        var relativeMatch = Regex.Match(dateStr, @"(\d+)\s+(minute|hour|day|week|month|year)s?\s+ago", RegexOptions.IgnoreCase);
        if (relativeMatch.Success)
        {
            var num = int.Parse(relativeMatch.Groups[1].Value);
            var unit = relativeMatch.Groups[2].Value.ToLowerInvariant();
            var now = DateTime.UtcNow;
            return unit switch
            {
                "minute" => now.AddMinutes(-num),
                "hour" => now.AddHours(-num),
                "day" => now.AddDays(-num),
                "week" => now.AddDays(-num * 7),
                "month" => now.AddDays(-num * 30),
                "year" => now.AddDays(-num * 365),
                _ => null
            };
        }

        // ISO format
        if (DateTime.TryParse(dateStr.Replace("Z", "+00:00"), out var isoDate))
            return isoDate;

        // Common patterns
        var patterns = new[] { "dd MMM yyyy", "MMM dd, yyyy", "yyyy-MM-dd", "dd/MM/yyyy", "MM/dd/yyyy" };
        foreach (var pattern in patterns)
        {
            if (DateTime.TryParseExact(dateStr, pattern, null, System.Globalization.DateTimeStyles.None, out var parsed))
                return parsed;
        }

        return null;
    }

    private static bool IsRecent(string dateStr, int maxHours = 24)
    {
        var dt = ParseDate(dateStr);
        if (!dt.HasValue)
            return true; // Include if can't parse

        var age = DateTime.UtcNow - dt.Value;
        return age.TotalHours < maxHours;
    }

    private static List<Article> FilterRecentArticles(List<Article> results)
    {
        var before = results.Count;
        var filtered = results.Where(a => IsRecent(a.IsoDate ?? a.Summary ?? "")).ToList();
        var after = filtered.Count;
        if (before != after)
            System.Diagnostics.Debug.WriteLine($"Date filter: kept {after}/{before} articles (last {Config.MAX_ARTICLE_AGE_HOURS}h)");
        return filtered;
    }

    public static string ExtractArticleId(Article item)
    {
        var title = item.Title ?? "";
        var link = item.Link ?? "";

        if (!string.IsNullOrEmpty(title))
        {
            using var md5 = System.Security.Cryptography.MD5.Create();
            var titleHash = BitConverter.ToString(md5.ComputeHash(System.Text.Encoding.UTF8.GetBytes(title.ToLowerInvariant()))).Replace("-", "").ToLowerInvariant().Substring(0, 12);
            var domain = "";
            if (!string.IsNullOrEmpty(link))
            {
                try
                {
                    var uri = new Uri(link);
                    domain = uri.Host.Replace("www.", "");
                }
                catch { }
            }
            return $"{titleHash}-{domain}";
        }

        return !string.IsNullOrEmpty(link) ? link.Split('/').Last() : Guid.NewGuid().ToString();
    }

    public static bool TitleIsDuplicate(string title, HashSet<string> seenTitles)
    {
        if (string.IsNullOrEmpty(title)) return true;
        var titleLower = title.ToLowerInvariant().Trim();
        return seenTitles.Any(seen => seen.Contains(titleLower) || titleLower.Contains(seen));
    }

    public static List<Article> SortByPubDate(List<Article> results)
    {
        return results.OrderByDescending(a => ParseDate(a.IsoDate ?? a.Summary ?? "") ?? DateTime.MinValue).ToList();
    }

    public static async Task<List<Article>> PickFreshArticlesAsync(List<Article> results, SeenStore seen, int limit = 10, Func<Task<List<Article>>> fetchMoreFn = null)
    {
        // Rank by priority
        results = ContentEngine.RankArticles(results);

        // Group similar stories
        var groups = ContentEngine.GroupSimilarStories(results, Config.STORY_SIMILARITY_THRESHOLD);

        var fresh = new List<Article>();
        var picked = new HashSet<string>();
        var pickedTitles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var group in groups)
        {
            if (fresh.Count >= limit) break;

            Article best = null;
            foreach (var item in group)
            {
                var articleId = ExtractArticleId(item);
                if (string.IsNullOrEmpty(articleId) || seen.Ids.Contains(articleId) || picked.Contains(articleId))
                    continue;

                var title = item.Title ?? "";
                if (string.IsNullOrEmpty(title)) continue;

                if (seen.Titles.Contains(title.ToLowerInvariant().Trim()) || TitleIsDuplicate(title, pickedTitles))
                    continue;

                if (string.IsNullOrEmpty(item.Thumbnail)) continue;

                if (string.IsNullOrEmpty(item.Link)) continue;

                if (best == null)
                    best = item;
                else if (!string.IsNullOrEmpty(item.Thumbnail) && string.IsNullOrEmpty(best.Thumbnail))
                    best = item;
            }

            if (best != null)
            {
                var articleId = ExtractArticleId(best);
                picked.Add(articleId);
                pickedTitles.Add(best.Title.ToLowerInvariant().Trim());
                best.RelatedSources = group.Count;
                fresh.Add(best);
            }
        }

        // Fetch more if needed
        if (fresh.Count < limit && fetchMoreFn != null)
        {
            var moreResults = await fetchMoreFn();
            if (moreResults.Any())
            {
                var moreFresh = await PickFreshArticlesAsync(moreResults, seen, limit - fresh.Count, fetchMoreFn);
                fresh.AddRange(moreFresh);
            }
        }

        return fresh.Take(limit).ToList();
    }

    private static async Task<bool> IsImageAccessible(string url)
    {
        if (string.IsNullOrEmpty(url)) return false;
        try
        {
            var request = new HttpRequestMessage(HttpMethod.Head, url);
            var response = await _httpClient.SendAsync(request);
            if (response.IsSuccessStatusCode) return true;

            // Try GET if HEAD fails
            response = await _httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
            return response.IsSuccessStatusCode || response.StatusCode == System.Net.HttpStatusCode.PartialContent || response.StatusCode == System.Net.HttpStatusCode.NotModified;
        }
        catch { return false; }
    }

    private static async Task<bool> IsSourceAccessible(string url)
    {
        if (string.IsNullOrEmpty(url)) return false;
        try
        {
            var request = new HttpRequestMessage(HttpMethod.Head, url);
            var response = await _httpClient.SendAsync(request);
            if (response.IsSuccessStatusCode) return true;

            response = await _httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
            return response.IsSuccessStatusCode;
        }
        catch { return false; }
    }

    // RSS Feed fetching
    private static readonly Dictionary<string, string[]> TopicToRssCategories = new()
    {
        { "bollywood", new[] { "bollywood" } },
        { "india_news", new[] { "india_news" } },
        { "india_politics", new[] { "india_politics" } }
    };

    public static async Task<List<Article>> FetchTopicAsync(string topic)
    {
        if (!TopicToRssCategories.TryGetValue(topic, out var categories))
            return new List<Article>();

        var articles = await FetchRssAsync(categories);
        System.Diagnostics.Debug.WriteLine($"RSS scraped {articles.Count} articles ({topic})");
        return articles;
    }

    private static async Task<List<Article>> FetchRssAsync(string[] categories)
    {
        var articles = new List<Article>();

        foreach (var (url, sourceName, category) in Config.RSS_FEEDS)
        {
            if (!categories.Contains(category))
                continue;

            try
            {
                var response = await _httpClient.GetAsync(url);
                var xmlContent = await response.Content.ReadAsStringAsync();
                var parsed = ParseRssFeed(xmlContent, sourceName, category);
                articles.AddRange(parsed.Take(30));
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"RSS error {url}: {ex.Message}");
            }
        }

        // Try to get og:image for entries missing thumbnails
        foreach (var article in articles.Where(a => string.IsNullOrEmpty(a.Thumbnail)))
        {
            article.Thumbnail = await GetOgImageAsync(article.Link);
        }

        return articles;
    }

    private static List<Article> ParseRssFeed(string xmlContent, string sourceName, string category)
    {
        var articles = new List<Article>();

        try
        {
            var doc = new XmlDocument();
            doc.LoadXml(xmlContent);

            var items = doc.SelectNodes("//item");
            if (items == null) return articles;

            foreach (XmlNode item in items)
            {
                var title = CleanHtml(item.SelectSingleNode("title")?.InnerText ?? "");
                var link = item.SelectSingleNode("link")?.InnerText ?? "";
                if (string.IsNullOrEmpty(title) || string.IsNullOrEmpty(link))
                    continue;

                var summary = CleanHtml(item.SelectSingleNode("description")?.InnerText ?? item.SelectSingleNode("summary")?.InnerText ?? "");
                var thumb = ExtractImageFromRss(item);
                var isoDate = item.SelectSingleNode("pubDate")?.InnerText ?? item.SelectSingleNode("published")?.InnerText ?? "";

                articles.Add(new Article
                {
                    Title = title,
                    Link = link,
                    Thumbnail = thumb,
                    Source = new InstaPostGenerator.Models.SourceInfo { Name = sourceName },
                    Summary = summary,
                    IsoDate = isoDate,
                    Category = category
                });
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error parsing RSS: {ex.Message}");
        }

        return articles;
    }

    private static string ExtractImageFromRss(XmlNode item)
    {
        // media:content
        var mediaContent = item.SelectSingleNode("media:content", GetNamespaceManager(item));
        if (mediaContent != null)
        {
            var url = mediaContent.Attributes?["url"]?.Value;
            if (!string.IsNullOrEmpty(url)) return url;
        }

        // media:thumbnail
        var mediaThumb = item.SelectSingleNode("media:thumbnail", GetNamespaceManager(item));
        if (mediaThumb != null)
        {
            var url = mediaThumb.Attributes?["url"]?.Value;
            if (!string.IsNullOrEmpty(url)) return url;
        }

        // enclosure
        var enclosure = item.SelectSingleNode("enclosure");
        if (enclosure != null)
        {
            var url = enclosure.Attributes?["url"]?.Value;
            if (!string.IsNullOrEmpty(url)) return url;
        }

        // Image in description
        var description = item.SelectSingleNode("description")?.InnerText ?? "";
        var imgMatch = Regex.Match(description, @"<img[^>]+src=[""']([^""']+)[""']");
        if (imgMatch.Success)
        {
            var url = imgMatch.Groups[1].Value;
            if (url.StartsWith("//")) url = "https:" + url;
            if (url.StartsWith("http")) return url;
        }

        return "";
    }

    private static XmlNamespaceManager GetNamespaceManager(XmlNode node)
    {
        var nsmgr = new XmlNamespaceManager(node.OwnerDocument.NameTable);
        nsmgr.AddNamespace("media", "http://search.yahoo.com/mrss/");
        nsmgr.AddNamespace("content", "http://purl.org/rss/1.0/modules/content/");
        return nsmgr;
    }

    private static string CleanHtml(string html)
    {
        if (string.IsNullOrEmpty(html)) return "";
        var text = Regex.Replace(html, "<[^>]+>", " ");
        text = System.Net.WebUtility.HtmlDecode(text);
        return Regex.Replace(text, @"\s+", " ").Trim();
    }

    private static async Task<string> GetOgImageAsync(string url)
    {
        if (string.IsNullOrEmpty(url)) return null;
        try
        {
            var response = await _httpClient.GetAsync(url);
            if (!response.IsSuccessStatusCode) return null;

            var html = await response.Content.ReadAsStringAsync();

            // Try various og:image patterns
            var patterns = new[]
            {
                @"<meta[^>]+property=[""']og:image[""'][^>]+content=[""']([^""']+)[""']",
                @"<meta[^>]+content=[""']([^""']+)[""'][^>]+property=[""']og:image[""']"
            };

            foreach (var pattern in patterns)
            {
                var match = Regex.Match(html, pattern, RegexOptions.IgnoreCase);
                if (match.Success)
                {
                    var val = System.Net.WebUtility.HtmlDecode(match.Groups[1].Value);
                    if (val.StartsWith("//")) val = "https:" + val;
                    if (val.StartsWith("http")) return val;
                }
            }
        }
        catch { }
        return null;
    }

    // Summarize article for caption
    public static async Task<string> SummarizeForCaptionAsync(string url)
    {
        if (string.IsNullOrEmpty(url)) return "";

        try
        {
            var response = await _httpClient.GetAsync(url);
            if (!response.IsSuccessStatusCode) return "";

            var html = await response.Content.ReadAsStringAsync();

            // Try meta tags first
            var metaPatterns = new[]
            {
                @"<meta[^>]+property=[""']og:description[""'][^>]+content=[""']([^""']+)[""']",
                @"<meta[^>]+name=[""']description[""'][^>]+content=[""']([^""']+)[""']",
                @"<meta[^>]+name=[""']twitter:description[""'][^>]+content=[""']([^""']+)[""']"
            };

            foreach (var pattern in metaPatterns)
            {
                var match = Regex.Match(html, pattern, RegexOptions.IgnoreCase);
                if (match.Success)
                {
                    var desc = System.Net.WebUtility.HtmlDecode(match.Groups[1].Value);
                    desc = Regex.Replace(desc, @"&[a-z]+;", " ");
                    desc = Regex.Replace(desc, @"\s+", " ");
                    if (desc.Length > 50)
                        return desc;
                }
            }

            // Fallback: extract from article content
            var doc = new HtmlDocument();
            doc.LoadHtml(html);

            // Remove script/style/nav/footer/aside/header/iframe/noscript
            var nodesToRemove = doc.DocumentNode.SelectNodes("//script | //style | //nav | //footer | //aside | //header | //iframe | //noscript");
            if (nodesToRemove != null)
            {
                foreach (var node in nodesToRemove)
                    node.Remove();
            }

            var articleTexts = new List<string>();
            var xpaths = new[]
            {
                "//article",
                "//div[contains(@class, 'article')]",
                "//div[contains(@class, 'content')]",
                "//main"
            };

            foreach (var xpath in xpaths)
            {
                var nodes = doc.DocumentNode.SelectNodes(xpath);
                if (nodes != null)
                {
                    foreach (var node in nodes)
                    {
                        var text = Regex.Replace(node.InnerText, @"\s+", " ").Trim();
                        if (text.Length > 50)
                            articleTexts.Add(text);
                    }
                }
            }

            if (articleTexts.Any())
            {
                var text = articleTexts.OrderByDescending(t => t.Length).First();
                return text;
            }
        }
        catch { }

        return "";
    }
}