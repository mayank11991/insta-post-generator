using System.Text.RegularExpressions;
using System.Globalization;
using InstaPostGenerator.Models;

namespace InstaPostGenerator.Services;

public static class ContentEngine
{
    private static readonly Random _random = new();
    private static readonly HashSet<string> _stopWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "a", "an", "the", "and", "or", "but", "in", "on", "at", "to", "for", "of", "with",
        "by", "from", "up", "about", "into", "through", "during", "before", "after",
        "above", "below", "over", "under", "again", "further", "then", "once", "here",
        "there", "when", "where", "why", "how", "all", "each", "few", "more", "most",
        "other", "some", "such", "no", "nor", "not", "only", "own", "same", "so",
        "than", "too", "very", "can", "will", "just", "should", "now", "is", "are",
        "was", "were", "be", "been", "being", "have", "has", "had", "do", "does",
        "did", "this", "that", "these", "those", "i", "you", "he", "she", "it",
        "we", "they", "me", "him", "her", "us", "them", "my", "your", "his", "its",
        "our", "their", "as", "if", "because", "while", "until", "unless"
    };

    private const double HIGHLIGHT_RATIO = 0.35;

    // Categories definition
    private static readonly Dictionary<string, CategoryInfo> Categories = new()
    {
        ["bollywood"] = new CategoryInfo
        {
            Label = "Bollywood & Hindi Entertainment",
            PriorityWeight = 10,
            Keywords = new[]
            {
                "bollywood", "hindi", "movie", "film", "trailer", "teaser", "release",
                "actor", "actress", "star", "celebrity", "celebrity", "gossip",
                "party", "spotted", "wedding", "relationship", "dating", "couple",
                "fashion", "style", "red carpet", "award", "photoshoot",
                "tv serial", "colors", "star plus", "zee tv", "sony tv",
                "web series", "ott", "netflix", "prime video", "hotstar", "zee5",
                "box office", "collection", "earning", "hit", "flop", "blockbuster",
                "shooting", "wrapped", "announced", "cast", "director", "producer",
                "meme", "viral", "trending", "funny", "hilarious", "dance", "song",
                "music", "album", "interview", "behind the scenes", "bts",
                "srk", "salman", "aamir", "ranbir", "ranveer", "alia", "deepika",
                "priyanka", "kareena", "ajay", "akshay", "kartik", "kiara",
                "kriti", "janhvi", "sara", "vicky", "ayushmann", "rajkummar",
                "karan johar", "sanjay leela bhansali", "amitabh", "dharmendra"
            },
            HookTemplates = new[]
            {
                "BREAKING: {entity} just dropped a bombshell...",
                "This just happened in Bollywood...",
                "Nobody saw this coming...",
                "BREAKING: {entity} makes huge announcement",
                "Just in: {entity} stuns everyone",
                "Bollywood is SHOCKED right now",
                "This is huge: {entity} just...",
                "ALERT: Major Bollywood update just dropped",
                "{entity} trailer just dropped and it looks insane",
                "First look: {entity} in an avatar you've never seen",
                "Everything we know about {entity} so far",
                "The wait is OVER — {entity} trailer is here",
                "{entity} just revealed something massive",
                "This is the Bollywood movie everyone's waiting for",
                "{entity} release date just got confirmed",
                "{entity} just broke the internet",
                "Why everyone is talking about {entity}",
                "This {entity} photo is going viral",
                "{entity} just made headlines for this reason",
                "The real story behind {entity}'s latest move",
                "Nobody expected this from {entity}",
                "{entity} just surprised everyone",
                "The internet can't stop talking about {entity}"
            },
            Hashtags = new[]
            {
                "#Bollywood", "#BollywoodNews", "#HindiCinema",
                "#EntertainmentNews", "#BreakingNews", "#Trending",
                "#BollywoodUpdates", "#IndianEntertainment",
                "#BollywoodMovies", "#BollywoodCelebrities",
                "#BollywoodGossip", "#TVSerials", "#OTT"
            },
            CTAs = new[]
            {
                "What do you think about this?",
                "Did you see this coming?",
                "Your thoughts on this development?",
                "Share this if you're shocked too!",
                "Tag someone who needs to see this!",
                "Would you watch this movie?",
                "Hit or flop — what's your prediction?",
                "Which movie are you most excited for?",
                "Rate this trailer 1-10!",
                "Tag your movie buddy!"
            },
            TemplateIds = new[] { 14 }
        },
        ["india_news"] = new CategoryInfo
        {
            Label = "India Latest News & Headlines",
            PriorityWeight = 9,
            Keywords = new[]
            {
                "india", "breaking", "latest", "news", "headlines", "today",
                "update", "confirmed", "announced", "declared", "revealed",
                "modi", "government", "parliament", "supreme court", "high court",
                "election", "bjp", "congress", "aap", "policy", "scheme",
                "economy", "gdp", "inflation", "budget", "tax", "rupee",
                "weather", "monsoon", "flood", "earthquake", "cyclone",
                "crime", "police", "arrested", "investigation", "court",
                "health", "covid", "vaccine", "hospital", "disease",
                "education", "university", "exam", "result", "admission",
                "sports", "cricket", "ipl", "olympics", "medal", "team india",
                "technology", "ai", "startup", "funding", "ipo", "unicorn",
                "infrastructure", "highway", "railway", "metro", "airport",
                "environment", "pollution", "climate", "green energy"
            },
            HookTemplates = new[]
            {
                "BREAKING: Major development in India right now...",
                "This just happened in India — you need to know",
                "Nobody saw this coming: {entity} makes huge move",
                "Just in: {entity} announces major decision",
                "India is talking about this right now...",
                "ALERT: Big news from {entity} just dropped",
                "The truth about {entity} — what's really happening?",
                "What {entity} did next left everyone shocked",
                "This {entity} update changes everything",
                "Major headline: {entity} just confirmed..."
            },
            Hashtags = new[]
            {
                "#IndiaNews", "#BreakingNews", "#LatestNews",
                "#IndiaHeadlines", "#TrendingInIndia", "#NewsToday",
                "#IndianNews", "#CurrentAffairs", "#IndiaUpdates"
            },
            CTAs = new[]
            {
                "What's your take on this?",
                "Did you see this coming?",
                "Share your thoughts below!",
                "Tag someone who needs to see this!",
                "Stay informed — follow for more!"
            },
            TemplateIds = new[] { 14 }
        },
        ["india_politics"] = new CategoryInfo
        {
            Label = "India Politics - Big Headlines",
            PriorityWeight = 9,
            Keywords = new[]
            {
                "modi", "narendra modi", "pm modi", "prime minister",
                "rahul gandhi", "congress", "bjp", "aam aadmi party", "aap",
                "amit shah", "arvind kejriwal", "mamata banerjee", "nitish kumar",
                "yogi adityanath", "election", "poll", "voting", "result",
                "parliament", "loksabha", "rajyasabha", "bill", "act",
                "supreme court", "judgement", "verdict", "hearing",
                "policy", "scheme", "yojana", "budget", "finance minister",
                "minister", "cabinet", "portfolio", "resign", "appointed",
                "alliance", "nda", "india alliance", "coalition",
                "protest", "rally", "campaign", "manifesto", "promise",
                "corruption", "scam", "investigation", "ed", "cbi", "raid"
            },
            HookTemplates = new[]
            {
                "BREAKING: Major political shakeup in India...",
                "This just happened in Indian politics...",
                "Nobody saw this coming: {entity} makes big move",
                "Just in: {entity} announces major political decision",
                "Indian politics is BUZZING right now...",
                "ALERT: Big political news from {entity} just dropped",
                "The truth about {entity}'s latest political move",
                "What {entity} did next left everyone shocked",
                "This political update from {entity} changes everything",
                "Major headline: {entity} just confirmed..."
            },
            Hashtags = new[]
            {
                "#IndianPolitics", "#PoliticsNews", "#BreakingNews",
                "#Modi", "#BJP", "#Congress", "#Election2024",
                "#Parliament", "#SupremeCourt", "#PoliticalNews",
                "#IndiaPolitics", "#CurrentAffairs"
            },
            CTAs = new[]
            {
                "What's your take on this?",
                "Did you see this coming?",
                "Share your thoughts below!",
                "Tag someone who follows politics!",
                "Stay informed — follow for more!"
            },
            TemplateIds = new[] { 14 }
        }
    };

    // Series definitions
    private static readonly Dictionary<string, SeriesInfo> Series = new()
    {
        ["bollywood_daily"] = new SeriesInfo { Name = "Bollywood Daily", BestFor = new[] { "bollywood" }, Description = "Daily Bollywood & Hindi entertainment roundup" },
        ["bollywood_breaking"] = new SeriesInfo { Name = "Bollywood Breaking", BestFor = new[] { "bollywood" }, Description = "Breaking Bollywood news & updates" },
        ["india_news_daily"] = new SeriesInfo { Name = "India News Today", BestFor = new[] { "india_news" }, Description = "Latest India headlines & breaking news" },
        ["india_news_breaking"] = new SeriesInfo { Name = "India Breaking News", BestFor = new[] { "india_news" }, Description = "Urgent India news alerts" },
        ["politics_daily"] = new SeriesInfo { Name = "Politics Daily", BestFor = new[] { "india_politics" }, Description = "Daily Indian politics roundup" },
        ["politics_breaking"] = new SeriesInfo { Name = "Politics Breaking", BestFor = new[] { "india_politics" }, Description = "Breaking political developments" }
    };

    // Celebrity names for entity extraction
    private static readonly string[] CelebrityNames = new[]
    {
        "shah rukh khan", "srk", "salman khan", "aamir khan",
        "akshay kumar", "ranbir kapoor", "ranveer singh", "hrithik roshan",
        "tiger shroff", "varun dhawan", "sidharth malhotra",
        "ayushmann khurrana", "kartik aaryan", "rajkummar rao", "vicky kaushal",
        "shahid kapoor", "aditya roy kapur", "arjun rampal",
        "priyanka chopra", "deepika padukone", "katrina kaif", "alia bhatt",
        "kareena kapoor", "kareena kapoor khan", "karisma kapoor",
        "anushka sharma", "kiara advani", "kriti sanon", "janhvi kapoor",
        "sara ali khan", "shanaya kapoor", "mouni roy", "kajol",
        "madhuri dixit", "juhi chawla", "hema malini", "rekha",
        "amitabh bachchan", "jeetendra", "dharmendra", "sanjay dutt",
        "anil kapoor", "bobby deol", "sunny deol", "ajay devgn",
        "saif ali khan", "twinkle khanna",
        "karan johar", "sanjay leela bhansali", "subhash ghai",
        "yash chopra", "aditya chopra",
        "nora fatehi", "malaika arora", "vaani kapoor",
        "disha patani", "daisy shah", "jacqueline fernandez",
        "nawazuddin siddiqui", "pankaj tripathi",
        "manoj bajpayee", "jaideep ahlawat", "vikrant massey",
        "satish kaushik", "paresh rawal", "boman irani",
        "john abraham", "abhishek bachchan", "aishwarya rai",
        "aishwarya rai bachchan", "kangana ranaut", "bhumi pednekar",
        "taapsee pannu", "radhika apte", "sobhita dhulipala",
        "radhika madan", "sanya malhotra", "fatima sana shaikh"
    };

    private static readonly string[] MovieKeywords = new[]
    {
        "animal", "pathaan", "jawan", "dunki", "tiger 3", "gadar 2",
        "omg 2", "rocky rani", "bhediya", "fighter", "crew", "kill",
        "stree 2", "vicky vidya", "bhool bhulaiyaa 3", "singham again",
        "the kerala story", "12th fail", "sam bahadur",
        "raees", "padmaavat", "bajirao mastani",
        "gangubai kathiawadi", "brahmastra", "rrr", "kgf", "pushpa",
        "kalki", "race 3", "bodyguard", "ek tha tiger", "war",
        "kick", "prem ratan dhan payo", "sultan", "tiger zinda hai",
        "zero", "laal singh chaddha", "83", "soorarai pottru",
        "master", "beast", "valimai", "ponniyin selvan",
        "tumbbad", "stree", "dream girl", "chhaava"
    };

    private static readonly string[] Politicians = new[]
    {
        "modi", "narendra modi", "rahul gandhi", "amit shah",
        "arvind kejriwal", "mamata banerjee", "nitish kumar",
        "yogi adityanath"
    };

    private class CategoryInfo
    {
        public string Label { get; set; } = "";
        public int PriorityWeight { get; set; }
        public string[] Keywords { get; set; } = Array.Empty<string>();
        public string[] HookTemplates { get; set; } = Array.Empty<string>();
        public string[] Hashtags { get; set; } = Array.Empty<string>();
        public string[] CTAs { get; set; } = Array.Empty<string>();
        public int[] TemplateIds { get; set; } = Array.Empty<int>();
    }

    private class SeriesInfo
    {
        public string Name { get; set; } = "";
        public string[] BestFor { get; set; } = Array.Empty<string>();
        public string Description { get; set; } = "";
    }

    public static Entities ExtractEntities(string text)
    {
        var textLower = text.ToLowerInvariant();
        var foundCelebs = new List<string>();
        var foundMovies = new List<string>();

        foreach (var name in CelebrityNames)
        {
            if (textLower.Contains(name))
                foundCelebs.Add(ToTitleCase(name));
        }

        foreach (var movie in MovieKeywords)
        {
            if (textLower.Contains(movie.Trim().ToLowerInvariant()))
                foundMovies.Add(movie.Trim());
        }

        return new Entities
        {
            Celebrities = foundCelebs.Distinct().ToList(),
            Movies = foundMovies.Distinct().ToList(),
            Primary = foundCelebs.FirstOrDefault() ?? foundMovies.FirstOrDefault() ?? ""
        };
    }

    private static string ToTitleCase(string input)
    {
        return CultureInfo.CurrentCulture.TextInfo.ToTitleCase(input.ToLower());
    }

    private static HashSet<string> GetTitleWords(string title)
    {
        var words = Regex.Matches(title.ToLowerInvariant(), @"[a-z]+")
            .Select(m => m.Value)
            .Where(w => !_stopWords.Contains(w) && w.Length > 2)
            .ToHashSet();
        return words;
    }

    public static double StorySimilarityScore(Article a1, Article a2)
    {
        var e1 = ExtractEntities(a1.Title);
        var e2 = ExtractEntities(a2.Title);

        double score = 0.0;

        // Celebrity overlap
        var celebs1 = e1.Celebrities.Select(c => c.ToLowerInvariant()).ToHashSet();
        var celebs2 = e2.Celebrities.Select(c => c.ToLowerInvariant()).ToHashSet();
        if (celebs1.Count > 0 && celebs2.Count > 0)
        {
            var overlap = celebs1.Intersect(celebs2).Count();
            score += 0.5 * overlap / Math.Max(celebs1.Union(celebs2).Count(), 1);
        }

        // Movie overlap
        var movies1 = e1.Movies.Select(m => m.ToLowerInvariant()).ToHashSet();
        var movies2 = e2.Movies.Select(m => m.ToLowerInvariant()).ToHashSet();
        if (movies1.Count > 0 && movies2.Count > 0)
        {
            var overlap = movies1.Intersect(movies2).Count();
            score += 0.5 * overlap / Math.Max(movies1.Union(movies2).Count(), 1);
        }

        // Word overlap fallback
        var w1 = GetTitleWords(a1.Title);
        var w2 = GetTitleWords(a2.Title);
        if (w1.Count > 0 && w2.Count > 0)
        {
            var wordOverlap = (double)w1.Intersect(w2).Count() / Math.Max(w1.Union(w2).Count(), 1);
            score += 0.3 * wordOverlap;
        }

        return Math.Min(score, 1.0);
    }

    public static List<List<Article>> GroupSimilarStories(List<Article> articles, double threshold = 0.5)
    {
        var used = new HashSet<int>();
        var groups = new List<List<Article>>();

        for (int i = 0; i < articles.Count; i++)
        {
            if (used.Contains(i)) continue;
            var group = new List<Article> { articles[i] };
            used.Add(i);

            for (int j = 0; j < articles.Count; j++)
            {
                if (used.Contains(j)) continue;
                if (StorySimilarityScore(articles[i], articles[j]) >= threshold)
                {
                    group.Add(articles[j]);
                    used.Add(j);
                }
            }
            groups.Add(group);
        }

        return groups;
    }

    public static (string Category, double Confidence) ClassifyCategory(Article article)
    {
        var title = (article.Title ?? "").ToLowerInvariant();
        var summary = (article.Summary ?? "").ToLowerInvariant();
        var text = $"{title} {summary}";

        var scores = new Dictionary<string, int>();
        foreach (var cat in Categories)
        {
            int score = 0;
            foreach (var kw in cat.Value.Keywords)
            {
                if (text.Contains(kw.ToLowerInvariant()))
                    score += 1;
            }
            foreach (var kw in cat.Value.Keywords)
            {
                if (title.Contains(kw.ToLowerInvariant()))
                    score += 2;
            }
            scores[cat.Key] = score;
        }

        if (!scores.Any() || scores.Values.Max() == 0)
            return ("bollywood", 0.0);

        var best = scores.OrderByDescending(x => x.Value).First().Key;
        var total = scores.Values.Sum();
        var confidence = total > 0 ? (double)scores[best] / total : 0;
        return (best, confidence);
    }

    public static int ScorePriority(Article article, string category)
    {
        var catInfo = Categories.GetValueOrDefault(category, Categories["bollywood"]);
        var baseScore = catInfo.PriorityWeight;

        var title = (article.Title ?? "").ToLowerInvariant();

        // Urgency boost
        int urgencyBoost = 0;
        foreach (var kw in new[] { "breaking", "just in", "confirmed", "exclusive", "shocking" })
        {
            if (title.Contains(kw))
            {
                urgencyBoost = 2;
                break;
            }
        }

        // Recency boost
        int recencyBoost = 0;
        var iso = article.IsoDate ?? article.Summary ?? "";
        if (iso.Contains("2026") || iso.Contains("2025"))
            recencyBoost = 1;

        // Entity boost
        var entities = ExtractEntities(title);
        int entityBoost = string.IsNullOrEmpty(entities.Primary) ? 0 : 1;

        var raw = baseScore + urgencyBoost + recencyBoost + entityBoost;
        return Math.Max(1, Math.Min(10, raw));
    }

    public static string SelectSeries(string category, List<string> recentSeries)
    {
        var candidates = new List<string>();
        foreach (var s in Series)
        {
            if (s.Value.BestFor.Contains(category))
            {
                int weight = recentSeries.Contains(s.Key) ? 1 : 3;
                for (int i = 0; i < weight; i++)
                    candidates.Add(s.Key);
            }
        }

        if (!candidates.Any())
            return "bollywood_daily";

        return candidates[_random.Next(candidates.Count)];
    }

    public static string GenerateHook(Article article, string category)
    {
        var title = (article.Title ?? "").Trim();
        var entities = ExtractEntities(title);
        var templates = Categories.GetValueOrDefault(category, Categories["bollywood"]).HookTemplates;

        var entity = entities.Primary;
        if (string.IsNullOrEmpty(entity) || entity.Length > 40)
        {
            entity = title.Split(':')[0].Trim();
            if (entity.Length > 40)
                entity = entity.Substring(0, 40).TrimEnd();
        }

        var entity2 = "";
        if (entities.Celebrities.Count > 1)
            entity2 = entities.Celebrities[1];
        else if (entities.Movies.Count > 1)
            entity2 = entities.Movies[1];

        var template = templates[_random.Next(templates.Length)];
        var hook = template
            .Replace("{entity}", entity)
            .Replace("{entity2}", string.IsNullOrEmpty(entity2) ? "the other star" : entity2)
            .Replace("{number}", new[] { "3", "5", "7" }[_random.Next(3)]);

        return hook;
    }

    public static string GenerateCTA(string category, List<string> recentCTAs)
    {
        var pool = Categories.GetValueOrDefault(category, Categories["bollywood"]).CTAs;
        var available = pool.Where(c => !recentCTAs.Contains(c)).ToList();
        if (!available.Any())
            available = pool.ToList();
        return available[_random.Next(available.Count)];
    }

    public static string BuildSmartCaption(Article article, string category, string hook, string cta, List<string> hashtags, string summaryText = null)
    {
        var parts = new List<string>();

        // Hook
        parts.Add(hook);
        parts.Add("");

        // Summary / context
        if (!string.IsNullOrEmpty(summaryText))
        {
            parts.Add(summaryText);
            parts.Add("");
        }

        // Source
        if (!string.IsNullOrEmpty(article.Link))
        {
            parts.Add($"Source: {article.Link}");
            parts.Add("");
        }

        // CTA
        parts.Add(cta);

        return string.Join("\n", parts);
    }

    public static (bool Passed, List<string> Issues) QualityCheck(Article article, string category, string hook)
    {
        var issues = new List<string>();
        var warnings = new List<string>();

        var title = article.Title ?? "";
        var titleLower = title.ToLowerInvariant();
        var summaryLower = (article.Summary ?? "").ToLowerInvariant();
        var sourceName = (article.Source?.Name ?? "").ToLowerInvariant();

        // Category-specific relevance
        var (relevantSources, relevantKeywords) = GetRelevanceData(category);

        bool relevantSignal = false;
        foreach (var src in relevantSources)
        {
            if (sourceName.Contains(src))
            {
                relevantSignal = true;
                break;
            }
        }

        if (!relevantSignal)
        {
            foreach (var kw in relevantKeywords)
            {
                if (titleLower.Contains(kw))
                {
                    relevantSignal = true;
                    break;
                }
            }
        }

        if (!relevantSignal)
        {
            foreach (var kw in relevantKeywords.Take(20))
            {
                if (summaryLower.Contains(kw))
                {
                    relevantSignal = true;
                    break;
                }
            }
        }

        if (!relevantSignal)
        {
            warnings.Add($"Not clearly {Categories.GetValueOrDefault(category)?.Label ?? category}-related (proceeding anyway)");
        }

        if (title.Length < 10)
            issues.Add("Title too short");

        return (issues.Count == 0, issues.Concat(warnings).ToList());
    }

    private static (string[] Sources, string[] Keywords) GetRelevanceData(string category)
    {
        return category switch
        {
            "bollywood" => (new[]
            {
                "bollywood hungama", "koimoi", "pinkvilla", "bollywood shaadis",
                "tellychakkar", "bollywood", "filmfare", "missmalini",
                "instantbollywood", "spotboye", "pune mirror", "times of india",
                "indian express", "ndtv", "hindustan times", "deccan herald"
            }, new[]
            {
                "bollywood", "hindi", "film", "movie", "actor", "actress", "star",
                "director", "producer", "celebrity", "ott", "netflix", "prime video",
                "sony", "zee", "colors", "star plus", "zee tv", "hotstar", "cinema",
                "tv serial", "web series", "trailer", "teaser", "release",
                "box office", "collection", "wedding", "relationship",
                "controversy", "award", "red carpet", "party", "spotted",
                "srk", "salman", "aamir", "ranbir", "ranveer", "alia",
                "deepika", "priyanka", "kareena", "ajay", "akshay",
                "nawazuddin", "vicky kaushal", "ayushmann", "kartik",
                "tiger", "varun", "sidharth", "rajkummar", "pankaj",
                "karan johar", "sanjay leela bhansali", "amitabh",
                "tumbbad", "stree", "pathaan", "jawan", "animal",
                "chhaava", "dunki", "fighter", "crew", "kill"
            }),

            "india_news" => (new[]
            {
                "times of india", "indian express", "the hindu", "hindustan times",
                "ndtv", "news18", "india today", "reuters", "pti", "ani",
                "livemint", "business standard", "economic times", "deccan herald",
                "the print", "scroll", "wire", "quint", "firstpost"
            }, new[]
            {
                "india", "breaking", "latest", "news", "headlines", "today",
                "update", "confirmed", "announced", "declared", "revealed",
                "modi", "government", "parliament", "supreme court", "high court",
                "election", "bjp", "congress", "aap", "policy", "scheme",
                "economy", "gdp", "inflation", "budget", "tax", "rupee",
                "weather", "monsoon", "flood", "earthquake", "cyclone",
                "crime", "police", "arrested", "investigation", "court",
                "health", "covid", "vaccine", "hospital", "disease",
                "education", "university", "exam", "result", "admission",
                "sports", "cricket", "ipl", "olympics", "medal", "team india",
                "technology", "ai", "startup", "funding", "ipo", "unicorn",
                "infrastructure", "highway", "railway", "metro", "airport"
            }),

            "india_politics" => (new[]
            {
                "times of india", "indian express", "the hindu", "hindustan times",
                "ndtv", "news18", "india today", "reuters", "pti", "ani",
                "livemint", "business standard", "economic times", "deccan herald",
                "the print", "scroll", "wire", "quint", "firstpost"
            }, new[]
            {
                "modi", "narendra modi", "pm modi", "prime minister",
                "rahul gandhi", "congress", "bjp", "aam aadmi party", "aap",
                "amit shah", "arvind kejriwal", "mamata banerjee", "nitish kumar",
                "yogi adityanath", "election", "poll", "voting", "result",
                "parliament", "loksabha", "rajyasabha", "bill", "act",
                "supreme court", "judgement", "verdict", "hearing",
                "policy", "scheme", "yojana", "budget", "finance minister",
                "minister", "cabinet", "portfolio", "resign", "appointed",
                "alliance", "nda", "india alliance", "coalition",
                "protest", "rally", "campaign", "manifesto", "promise",
                "corruption", "scam", "investigation", "ed", "cbi", "raid"
            }),

            _ => (Array.Empty<string>(), Array.Empty<string>())
        };
    }

    public static ProcessedArticle ProcessArticle(Article article, ContentMixTracker mix)
    {
        var (category, confidence) = ClassifyCategory(article);
        var priority = ScorePriority(article, category);
        var hook = GenerateHook(article, category);
        var cta = GenerateCTA(category, mix.RecentCTAs);
        var series = SelectSeries(category, mix.RecentSeries);
        var seriesInfo = Series.GetValueOrDefault(series);
        var catInfo = Categories.GetValueOrDefault(category, Categories["bollywood"]);
        var templateIds = catInfo.TemplateIds;
        var (passed, issues) = QualityCheck(article, category, hook);

        mix.AddCategory(category);
        mix.AddSeries(series);
        mix.AddCTA(cta);

        var entities = ExtractEntities(article.Title);

        // Generate only 5 hashtags from title content
        var hashtags = GenerateHashtagsFromTitle(article.Title, entities, category);

        return new ProcessedArticle
        {
            Article = article,
            Category = category,
            CategoryLabel = catInfo.Label,
            Confidence = confidence,
            Priority = priority,
            Hook = hook,
            CTA = cta,
            Series = series,
            SeriesName = seriesInfo?.Name ?? series,
            Hashtags = hashtags,
            TemplateIds = templateIds,
            QualityPassed = passed,
            QualityIssues = issues,
            Entities = entities
        };
    }

    private static List<string> GenerateHashtagsFromTitle(string title, Entities entities, string category)
    {
        var hashtags = new List<string>();
        var titleLower = (title ?? "").ToLowerInvariant();

        // Add celebrity names as hashtags
        foreach (var celeb in entities.Celebrities.Take(2))
        {
            var tag = Regex.Replace(celeb, @"[^a-zA-Z0-9]", "");
            if (!string.IsNullOrEmpty(tag))
                hashtags.Add($"#{tag}");
        }

        // Add movie/series names as hashtags
        foreach (var movie in entities.Movies.Take(2))
        {
            var tag = Regex.Replace(movie, @"[^a-zA-Z0-9]", "");
            if (!string.IsNullOrEmpty(tag) && !hashtags.Contains($"#{tag}"))
                hashtags.Add($"#{tag}");
        }

        // Add category hashtag
        var categoryTag = category switch
        {
            "bollywood" => "#Bollywood",
            "india_news" => "#IndiaNews",
            "india_politics" => "#IndianPolitics",
            _ => "#News"
        };
        if (!hashtags.Contains(categoryTag))
            hashtags.Add(categoryTag);

        // Add #360buzz
        if (!hashtags.Contains("#360buzz"))
            hashtags.Add("#360buzz");

        // Fill remaining slots with important words from title
        if (hashtags.Count < 5)
        {
            var words = Regex.Matches(title ?? "", @"[A-Za-z]{3,}")
                .Select(m => m.Value)
                .Where(w => !_stopWords.Contains(w.ToLower()) && w.Length > 3)
                .Distinct()
                .ToList();

            foreach (var word in words)
            {
                if (hashtags.Count >= 5) break;
                var tag = $"#{word}";
                if (!hashtags.Contains(tag))
                    hashtags.Add(tag);
            }
        }

        return hashtags.Take(5).ToList();
    }

    public static List<Article> RankArticles(List<Article> articles)
    {
        var scored = articles.Select(a =>
        {
            var (cat, _) = ClassifyCategory(a);
            var pri = ScorePriority(a, cat);
            return (Priority: pri, Article: a);
        }).OrderByDescending(x => x.Priority).Select(x => x.Article).ToList();

        return scored;
    }
}

public class ContentMixTracker
{
    private readonly string _path;
    public ContentMixData Data { get; private set; } = new();

    public ContentMixTracker(string path = null)
    {
        _path = path ?? Config.GetSeenFile();
        Load();
    }

    public List<string> RecentCategories => Data.RecentCategories.TakeLast(10).ToList();
    public List<string> RecentSeries => Data.RecentSeries.TakeLast(5).ToList();
    public List<string> RecentCTAs => Data.RecentCTAs.TakeLast(5).ToList();

    public void AddCategory(string category)
    {
        Data.RecentCategories.Add(category);
        Data.RecentCategories = Data.RecentCategories.TakeLast(15).ToList();
    }

    public void AddSeries(string series)
    {
        Data.RecentSeries.Add(series);
        Data.RecentSeries = Data.RecentSeries.TakeLast(10).ToList();
    }

    public void AddCTA(string cta)
    {
        Data.RecentCTAs.Add(cta);
        Data.RecentCTAs = Data.RecentCTAs.TakeLast(8).ToList();
    }

    public void Save()
    {
        var json = Newtonsoft.Json.JsonConvert.SerializeObject(Data, Newtonsoft.Json.Formatting.Indented);
        File.WriteAllText(_path, json);
    }

    private void Load()
    {
        if (File.Exists(_path))
        {
            try
            {
                var json = File.ReadAllText(_path);
                Data = Newtonsoft.Json.JsonConvert.DeserializeObject<ContentMixData>(json) ?? new ContentMixData();
            }
            catch
            {
                Data = new ContentMixData();
            }
        }
    }
}