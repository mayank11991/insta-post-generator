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
        },
        ["bollywood_images"] = new CategoryInfo
        {
            Label = "Bollywood HD Images",
            PriorityWeight = 10,
            Keywords = new[]
            {
                "bollywood", "celebrity", "actor", "actress", "star", "portrait", "hd", "photo"
            },
            HookTemplates = new[]
            {
                "Stunning portrait of {entity} ✨",
                "{entity} looking absolutely gorgeous 📸",
                "This photo of {entity} is everything 💫",
                "{entity} serving looks as always 🔥",
                "Can't get over this {entity} portrait 😍",
                "HD perfection: {entity} in all their glory ✨",
                "{entity} just proving why they're a star 🌟",
                "Frame this photo of {entity} 🖼️",
                "{entity} radiating main character energy ✨",
                "This {entity} portrait is iconic 📸",
                "Absolutely mesmerizing: {entity} 💫",
                "{entity} looking like a dream ✨",
                "Pure stardom: {entity} 🌟",
                "Every angle is their best angle: {entity} 📸",
                "{entity} serving Bollywood royalty 👑"
            },
            Hashtags = new[]
            {
                "#Bollywood", "#BollywoodCelebrities", "#IndianCelebrities",
                "#BollywoodStars", "#CelebrityPortrait", "#HDPhoto",
                "#BollywoodFashion", "#IndianCinema", "#StarPower",
                "#360buzz"
            },
            CTAs = new[]
            {
                "Who's your favorite Bollywood star?",
                "This look is 🔥 — agree?",
                "Save this portrait! 📸",
                "Tag a fan of {entity}!",
                "Which celebrity portrait should be next?"
            },
            TemplateIds = new[] { 14 }
        },
        ["celebrity_portraits"] = new CategoryInfo
        {
            Label = "Celebrity HD Portraits",
            PriorityWeight = 9,
            Keywords = new[]
            {
                "celebrity", "actor", "actress", "portrait", "hd", "photo", "indian", "star"
            },
            HookTemplates = new[]
            {
                "Timeless beauty: {entity} ✨",
                "{entity} — a legend in every frame 📸",
                "Iconic portrait of {entity} 🌟",
                "{entity} defining elegance 💫",
                "This {entity} photo is pure art 🖼️",
                "HD portrait perfection: {entity} ✨",
                "{entity} capturing hearts since forever ❤️",
                "Frame-worthy: {entity} in HD 📸",
                "{entity} — Bollywood's eternal star 🌟",
                "Absolute perfection: {entity} 💫",
                "The magic of {entity} in one frame ✨",
                "{entity} looking absolutely timeless 👑",
                "Celebrity portrait goals: {entity} 📸",
                "{entity} serving pure elegance ✨",
                "Iconic. Legendary. {entity} 🌟"
            },
            Hashtags = new[]
            {
                "#CelebrityPortrait", "#IndianCelebrities", "#HDPhoto",
                "#BollywoodLegends", "#IconicStars", "#CelebrityPhotos",
                "#IndianCinema", "#StarPortrait", "#TimelessBeauty",
                "#360buzz"
            },
            CTAs = new[]
            {
                "Who's your all-time favorite star?",
                "This portrait is everything 💫",
                "Save for your celebrity collection! 📸",
                "Tag someone who loves {entity}!",
                "Which legend should we feature next?"
            },
            TemplateIds = new[] { 14 }
        },
        ["ai_news"] = new CategoryInfo
        {
            Label = "Artificial Intelligence News",
            PriorityWeight = 9,
            Keywords = new[]
            {
                "openai", "anthropic", "google deepmind", "meta ai", "china ai",
                "artificial intelligence", "machine learning", "chatgpt", "gpt", "claude",
                "gemini", "llama", "ai startup", "ai funding", "ai regulation",
                "ai safety", "agi", "large language model", "llm", "generative ai",
                "ai image", "ai video", "ai audio", "ai robot", "autonomous",
                "deep learning", "neural network", "transformer", "diffusion",
                "ai breakthrough", "ai research", "open source ai", "ai policy",
                "elon musk ai", "sam altman", "dario amodei", "demis hassabis"
            },
            HookTemplates = new[]
            {
                "BREAKING: {entity} just announced something huge...",
                "This AI news changes everything...",
                "Nobody saw this AI breakthrough coming...",
                "Just in: {entity} drops bombshell AI update",
                "AI world is SHOCKED right now...",
                "ALERT: Major AI news from {entity}",
                "The future just arrived: {entity} unveils...",
                "This {entity} AI update is massive",
                "AI will never be the same after this...",
                "{entity} just made history in AI"
            },
            Hashtags = new[]
            {
                "#AI", "#ArtificialIntelligence", "#MachineLearning",
                "#OpenAI", "#Anthropic", "#DeepMind", "#TechNews",
                "#AITrends", "#FutureTech", "#360buzz"
            },
            CTAs = new[]
            {
                "What's your take on this AI development?",
                "Will this change the AI landscape?",
                "Share your thoughts on AI!",
                "Tag someone interested in AI!",
                "Excited or scared about this AI news?"
            },
            TemplateIds = new[] { 14 }
        },
        ["bollywood_facts"] = new CategoryInfo
        {
            Label = "Bollywood Unknown Facts",
            PriorityWeight = 8,
            Keywords = new[]
            {
                "bollywood", "fact", "unknown", "secret", "behind the scenes",
                "trivia", "fun fact", "did you know", "movie fact", "actor fact",
                "bollywood history", "classic bollywood", "old bollywood",
                "bollywood controversy", "bollywood scandal", "bollywood secret",
                "movie trivia", "film trivia", "bollywood trivia", "celebrity fact"
            },
            HookTemplates = new[]
            {
                "Did you know this about {entity}?",
                "This Bollywood fact will blow your mind...",
                "Nobody knows this about {entity}...",
                "Bollywood secret revealed: {entity}...",
                "This unknown fact about {entity} is shocking...",
                "You won't believe this {entity} fact...",
                "Bollywood trivia: {entity} edition...",
                "The hidden truth about {entity}...",
                "This {entity} fact is mind-blowing...",
                "Nobody told you this about Bollywood..."
            },
            Hashtags = new[]
            {
                "#BollywoodFacts", "#BollywoodTrivia", "#UnknownFacts",
                "#BollywoodSecrets", "#DidYouKnow", "#FunFacts",
                "#BollywoodHistory", "#360buzz"
            },
            CTAs = new[]
            {
                "Did you know this fact?",
                "Share this with a Bollywood fan!",
                "Comment if you already knew this!",
                "Tag someone who loves Bollywood trivia!",
                "Which fact surprised you the most?"
            },
            TemplateIds = new[] { 14 }
        },
        ["quiz"] = new CategoryInfo
        {
            Label = "Bollywood Quiz",
            PriorityWeight = 8,
            Keywords = new[]
            {
                "quiz", "guess", "trivia", "test", "challenge",
                "bollywood quiz", "movie quiz", "celebrity quiz",
                "guess the movie", "guess the actor", "name the film"
            },
            HookTemplates = new[]
            {
                "🎯 QUIZ TIME! Can you guess this {entity}?",
                "Test your Bollywood knowledge!",
                "Only true fans can answer this!",
                "Can you guess {entity} from this clue?",
                "This quiz will test your Bollywood IQ!",
                "Think you know Bollywood? Prove it!",
                "Challenge: Can you name {entity}?",
                "Bollywood quiz: How well do you know {entity}?",
                "Only 1% can answer this correctly!",
                "Comment your answer below! 👇"
            },
            Hashtags = new[]
            {
                "#BollywoodQuiz", "#GuessTheMovie", "#BollywoodTrivia",
                "#QuizTime", "#TestYourKnowledge", "#BollywoodChallenge",
                "#360buzz"
            },
            CTAs = new[]
            {
                "Comment your answer below! 👇",
                "Can you guess it? Drop your answer!",
                "Tag someone who can solve this!",
                "Share your score in comments!",
                "Think you got it right?"
            },
            TemplateIds = new[] { 14 }
        },
        ["bollywood_teasers"] = new CategoryInfo
        {
            Label = "Bollywood Teasers",
            PriorityWeight = 9,
            Keywords = new[]
            {
                "bollywood", "movie teaser", "official teaser", "trailer",
                "film teaser", "first look", "movie clip", "song"
            },
            HookTemplates = new[]
            {
                "🎬 {entity} teaser just dropped!",
                "Watch this epic {entity} teaser!",
                "This {entity} looks INSANE!",
                "{entity} official teaser is here!",
                "You NEED to see this {entity} teaser!",
                "This {entity} moment is iconic!",
                "Viral alert: {entity} teaser! 🔥",
                "This {entity} is going to be HUGE!",
                "Can't wait for {entity}!",
                "{entity} just set the internet on fire! 🔥"
            },
            Hashtags = new[]
            {
                "#BollywoodTeaser", "#MovieTeaser", "#Bollywood",
                "#ViralReels", "#NewMovie", "#Reels",
                "#360buzz"
            },
            CTAs = new[]
            {
                "Watch till the end! 🔥",
                "Tag someone who loves {entity}!",
                "Share this reel!",
                "Comment your excitement!",
                "Follow for more such teasers!"
            },
            TemplateIds = new[] { 14 }
        },
        ["viral_trends"] = new CategoryInfo
        {
            Label = "Viral Trends",
            PriorityWeight = 9,
            Keywords = new[]
            {
                "viral", "trending", "funny", "comedy", "challenge",
                "trending video", "viral video", "funny video", "dance"
            },
            HookTemplates = new[]
            {
                "🔥 This is going VIRAL right now!",
                "Watch this before it blows up!",
                "This is the funniest thing today!",
                "Everyone is sharing this!",
                "You NEED to watch this viral reel!",
                "This is breaking the internet! 🔥",
                "Viral alert: Can't miss this!",
                "This is the most shared video today!",
                "Can't stop watching this!",
                "This is why internet is amazing! 🔥"
            },
            Hashtags = new[]
            {
                "#ViralTrends", "#Trending", "#Viral",
                "#Funny", "#TrendingNow", "#Reels",
                "#360buzz"
            },
            CTAs = new[]
            {
                "Tag someone who needs to see this!",
                "Share with your friends!",
                "Comment if this made you laugh!",
                "Follow for more viral content!",
                "Too good not to share!"
            },
            TemplateIds = new[] { 14 }
        },
        ["viral_paparazzi"] = new CategoryInfo
        {
            Label = "Viral Paparazzi",
            PriorityWeight = 9,
            Keywords = new[]
            {
                "paparazzi", "spotted", "celebrity", "airport",
                "bollywood", "party", "outing", "couple", "wedding"
            },
            HookTemplates = new[]
            {
                "📸 {entity} just got SPOTTED!",
                "Look what {entity} was doing!",
                "{entity} spotted and it's going viral!",
                "You won't believe where {entity} was spotted!",
                "This {entity} sighting is EVERYTHING!",
                "{entity} looking stunning as always! 🔥",
                "Viral alert: {entity} spotted! 📸",
                "Can't stop looking at {entity}!",
                "{entity} just made our day! ✨",
                "This {entity} moment is pure gold! 🔥"
            },
            Hashtags = new[]
            {
                "#Paparazzi", "#Spotted", "#BollywoodSpotted",
                "#CelebritySpotted", "#Viral", "#Reels",
                "#360buzz"
            },
            CTAs = new[]
            {
                "Tag someone who loves {entity}!",
                "Share this sighting!",
                "Comment your reaction!",
                "Follow for more celebrity updates!",
                "What do you think about this look?"
            },
            TemplateIds = new[] { 14 }
        },
        ["political_highlights"] = new CategoryInfo
        {
            Label = "Political Highlights",
            PriorityWeight = 8,
            Keywords = new[]
            {
                "politics", "modi", "parliament", "election",
                "political", "minister", "government", "debate"
            },
            HookTemplates = new[]
            {
                "🏛️ This political moment is going viral!",
                "Watch this {entity} political clip!",
                "This {entity} moment is iconic!",
                "{entity} just made this big move!",
                "You NEED to see this political reel!",
                "This {entity} speech is powerful!",
                "Viral alert: {entity} political clip! 🔥",
                "This {entity} moment is historic!",
                "Can't stop watching this political reel!",
                "{entity} just set the internet on fire! 🔥"
            },
            Hashtags = new[]
            {
                "#PoliticalHighlights", "#PoliticalNews", "#Modi",
                "#Parliament", "#IndianPolitics", "#Reels",
                "#360buzz"
            },
            CTAs = new[]
            {
                "What's your take on this?",
                "Tag someone who follows politics!",
                "Share this reel!",
                "Comment your thoughts!",
                "Follow for more political updates!"
            },
            TemplateIds = new[] { 14 }
        },
        ["ai_reels"] = new CategoryInfo
        {
            Label = "AI Reels",
            PriorityWeight = 8,
            Keywords = new[]
            {
                "ai", "artificial intelligence", "technology", "robot",
                "tech demo", "ai demo", "future tech", "innovation"
            },
            HookTemplates = new[]
            {
                "🤖 This AI tech is mind-blowing!",
                "Watch this {entity} AI in action!",
                "The future is HERE: {entity}!",
                "This tech demo will blow your mind!",
                "{entity} just changed AI forever!",
                "You won't believe this AI can do this!",
                "This {entity} tech is insane! 🔥",
                "AI just got REAL: {entity}!",
                "Watch AI do what humans can't!",
                "This {entity} innovation is next level!"
            },
            Hashtags = new[]
            {
                "#AITech", "#AIReels", "#AIDemo",
                "#FutureTech", "#Innovation", "#TechNews",
                "#360buzz"
            },
            CTAs = new[]
            {
                "What do you think about this tech?",
                "Tag a tech lover!",
                "Share this with someone who needs to see!",
                "Follow for more tech updates!",
                "Excited or scared about AI?"
            },
            TemplateIds = new[] { 14 }
        },
        ["webseries_reels"] = new CategoryInfo
        {
            Label = "Web Series Reels",
            PriorityWeight = 8,
            Keywords = new[]
            {
                "web series", "netflix", "amazon prime", "hotstar",
                "series trailer", "show trailer", "web show", "ott"
            },
            HookTemplates = new[]
            {
                "📺 This {entity} clip is mind-blowing!",
                "Watch this {entity} scene!",
                "This {entity} moment is iconic!",
                "{entity} just dropped this amazing clip!",
                "You NEED to see this {entity} reel!",
                "This {entity} scene is everything!",
                "Viral alert: {entity} clip! 🔥",
                "This {entity} moment is pure gold!",
                "Can't stop watching this {entity} reel!",
                "{entity} just set the internet on fire! 🔥"
            },
            Hashtags = new[]
            {
                "#WebSeriesReels", "#NetflixSeries", "#AmazonPrime",
                "#SeriesReels", "#OTTShows", "#Reels",
                "#360buzz"
            },
            CTAs = new[]
            {
                "Have you watched {entity}?",
                "Tag someone who loves this series!",
                "Share this reel!",
                "Comment your favorite scene!",
                "Follow for more series updates!"
            },
            TemplateIds = new[] { 14 }
        }
    };

    // Series definitions
    private static readonly Dictionary<string, SeriesInfo> Series = new()
    {
        ["bollywood_daily"] = new SeriesInfo { Name = "Bollywood Daily", BestFor = new[] { "bollywood" }, Description = "Daily Bollywood & Hindi entertainment roundup" },
        ["bollywood_breaking"] = new SeriesInfo { Name = "Bollywood Breaking", BestFor = new[] { "bollywood" }, Description = "Breaking Bollywood news & updates" },
        ["ai_daily"] = new SeriesInfo { Name = "AI Daily", BestFor = new[] { "ai_news" }, Description = "Latest AI & tech news" },
        ["ai_breaking"] = new SeriesInfo { Name = "AI Breaking", BestFor = new[] { "ai_news" }, Description = "Breaking AI developments" },
        ["politics_daily"] = new SeriesInfo { Name = "Politics Daily", BestFor = new[] { "india_politics" }, Description = "Daily Indian politics roundup" },
        ["politics_breaking"] = new SeriesInfo { Name = "Politics Breaking", BestFor = new[] { "india_politics" }, Description = "Breaking political developments" },
        ["facts_daily"] = new SeriesInfo { Name = "Bollywood Facts", BestFor = new[] { "bollywood_facts" }, Description = "Unknown Bollywood facts & trivia" },
        ["quiz_daily"] = new SeriesInfo { Name = "Bollywood Quiz", BestFor = new[] { "quiz" }, Description = "Daily Bollywood quiz challenge" },
        ["reels_bollywood_teasers"] = new SeriesInfo { Name = "Bollywood Teasers", BestFor = new[] { "bollywood_teasers" }, Description = "Latest Bollywood teasers & trailers" },
        ["reels_viral_trends"] = new SeriesInfo { Name = "Viral Trends", BestFor = new[] { "viral_trends" }, Description = "Trending viral clips" },
        ["reels_paparazzi"] = new SeriesInfo { Name = "Viral Paparazzi", BestFor = new[] { "viral_paparazzi" }, Description = "Celebrity spotted moments" },
        ["reels_politics"] = new SeriesInfo { Name = "Political Highlights", BestFor = new[] { "political_highlights" }, Description = "Political moments & clips" },
        ["reels_ai"] = new SeriesInfo { Name = "AI Reels", BestFor = new[] { "ai_reels" }, Description = "Latest AI & tech clips" },
        ["reels_webseries"] = new SeriesInfo { Name = "Web Series Reels", BestFor = new[] { "webseries_reels" }, Description = "Best web series moments" }
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
        _path = path ?? Config.GetContentMixFile();
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