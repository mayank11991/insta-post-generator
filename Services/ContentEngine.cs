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
        // ═══════════════════════════════════════════════════════════════
        // BOLLYWOOD CATEGORIES (1–25)
        // ═══════════════════════════════════════════════════════════════
        ["breaking_bollywood"] = new CategoryInfo
        {
            Label = "Breaking Bollywood News",
            PriorityWeight = 9,
            Keywords = new[] { "bollywood", "movie", "celebrity", "news", "latest", "breaking", "viral", "trending" },
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
                "First look: {entity} in an avatar you've never seen"
            },
            Hashtags = new[] { "#Bollywood", "#BreakingNews", "#BollywoodNews", "#CelebrityNews", "#Trending", "#Viral", "#Entertainment" },
            CTAs = new[]
            {
                "What do you think about this?",
                "Did you see this coming?",
                "Share this if you're shocked too!",
                "Tag someone who needs to see this!",
                "Follow for more breaking updates!"
            },
            TemplateIds = new[] { 14 }
        },
        ["celebrity_viral"] = new CategoryInfo
        {
            Label = "Celebrity Viral Moment",
            PriorityWeight = 9,
            Keywords = new[] { "celebrity", "viral", "video", "photo", "trending", "funny", "spotted" },
            HookTemplates = new[]
            {
                "This {entity} moment is going VIRAL!",
                "Watch this before it blows up!",
                "Everyone is sharing this {entity} clip!",
                "You NEED to see this {entity} moment!",
                "This is the funniest {entity} thing today!",
                "{entity} just broke the internet!",
                "Viral alert: {entity} can't stop talking about this!",
                "This {entity} video has everyone shocked!",
                "Can't stop watching this {entity} clip!",
                "The internet can't stop laughing at {entity}!"
            },
            Hashtags = new[] { "#Viral", "#Celebrity", "#Trending", "#Bollywood", "#ViralVideo", "#Funny", "#360buzz" },
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
        ["movie_announcement"] = new CategoryInfo
        {
            Label = "Movie Announcement",
            PriorityWeight = 9,
            Keywords = new[] { "movie", "announcement", "new film", "cast", "director", "release", "launch" },
            HookTemplates = new[]
            {
                "BREAKING: {entity} movie just announced!",
                "This is the Bollywood movie everyone's waiting for",
                "{entity} release date just got confirmed",
                "HUGE announcement: {entity} is coming!",
                "{entity} just revealed something massive",
                "Everything we know about {entity} so far",
                "The wait is OVER — {entity} is official!",
                "{entity} just made a blockbuster announcement",
                "This {entity} project is going to be EPIC!",
                "Just in: {entity} confirms new movie!"
            },
            Hashtags = new[] { "#Bollywood", "#MovieAnnouncement", "#NewMovie", "#ComingSoon", "#BollywoodNews", "#Film" },
            CTAs = new[]
            {
                "Are you excited for this movie?",
                "Which actor are you most excited to see?",
                "Share this announcement!",
                "Tag your movie buddy!",
                "Drop a 🔥 if you're excited!"
            },
            TemplateIds = new[] { 14 }
        },
        ["celebrity_controversy"] = new CategoryInfo
        {
            Label = "Celebrity Controversy",
            PriorityWeight = 9,
            Keywords = new[] { "controversy", "fight", "statement", "viral", "debate", "alleged" },
            HookTemplates = new[]
            {
                "BREAKING: {entity} at center of huge controversy!",
                "Nobody expected this from {entity}...",
                "{entity} just made a shocking statement!",
                "The truth about {entity}'s latest controversy",
                "This {entity} scandal is going VIRAL!",
                "What {entity} did next left everyone shocked",
                "ALERT: {entity} controversy just escalated!",
                "{entity} just dropped a bombshell statement!",
                "This {entity} drama is getting intense!",
                "The real story behind {entity}'s controversy"
            },
            Hashtags = new[] { "#Controversy", "#Celebrity", "#Bollywood", "#Trending", "#Viral", "#Breaking" },
            CTAs = new[]
            {
                "What's your take on this?",
                "Whose side are you on?",
                "Share your thoughts below!",
                "Tag someone who needs to see this!",
                "Do you think this is fair?"
            },
            TemplateIds = new[] { 14 }
        },
        ["box_office_update"] = new CategoryInfo
        {
            Label = "Box Office Update",
            PriorityWeight = 9,
            Keywords = new[] { "box office", "collection", "hit", "flop", "budget", "earning", "blockbuster" },
            HookTemplates = new[]
            {
                "{entity} box office collection just dropped!",
                "Hit or flop? {entity} numbers are in!",
                "{entity} just crossed a MASSIVE milestone!",
                "Blockbuster alert: {entity} smashes records!",
                "The real story behind {entity}'s box office numbers",
                "{entity} day 1 collection will SHOCK you!",
                "Is {entity} a hit or flop? Here's the truth!",
                "{entity} just crushed the box office!",
                "This {entity} box office update changes everything!",
                "{entity} earned HOW much?! 😱"
            },
            Hashtags = new[] { "#BoxOffice", "#Bollywood", "#HitOrFlop", "#Collection", "#Blockbuster", "#MovieReview" },
            CTAs = new[]
            {
                "Hit or flop — what's your prediction?",
                "Will you watch this movie?",
                "Share your verdict!",
                "Tag someone who loves box office stats!",
                "Rate this movie 1-10!"
            },
            TemplateIds = new[] { 14 }
        },
        ["box_office_battle"] = new CategoryInfo
        {
            Label = "Box Office Battle",
            PriorityWeight = 9,
            Keywords = new[] { "box office", "battle", "comparison", "vs", "earnings", "collection", "clash" },
            HookTemplates = new[]
            {
                "{entity} vs the competition — who's winning?",
                "Box office BATTLE: {entity} dominates!",
                "This {entity} comparison is mind-blowing!",
                "{entity} just crushed the competition!",
                "Who won the box office battle? {entity} or rivals?",
                "{entity} box office numbers vs expectations!",
                "The ultimate {entity} showdown at the box office!",
                "{entity} vs the world — box office report!",
                "This {entity} battle is making headlines!",
                "Box office clash: {entity} takes the lead!"
            },
            Hashtags = new[] { "#BoxOffice", "#Battle", "#Bollywood", "#Collection", "#HitVsFlop", "#MovieClash" },
            CTAs = new[]
            {
                "Which movie won this battle?",
                "Share your box office prediction!",
                "Tag someone who follows box office!",
                "What's your verdict?",
                "Follow for more box office updates!"
            },
            TemplateIds = new[] { 14 }
        },
        ["celebrity_then_now"] = new CategoryInfo
        {
            Label = "Celebrity Then vs Now",
            PriorityWeight = 9,
            Keywords = new[] { "then vs now", "transformation", "old", "young", "comparison", "journey" },
            HookTemplates = new[]
            {
                "Look how {entity} has changed over the years!",
                "From then to now: {entity}'s incredible transformation!",
                "{entity} THEN vs NOW — you won't believe the difference!",
                "This {entity} transformation is jaw-dropping!",
                "From nobody to superstar: {entity}'s journey!",
                "{entity} then and now — the glow up is REAL!",
                "Nobody expected {entity} to look like this now!",
                "The incredible evolution of {entity}!",
                "This {entity} then vs now will blow your mind!",
                "{entity} has changed SO much — look at this!"
            },
            Hashtags = new[] { "#ThenVsNow", "#Transformation", "#Bollywood", "#Celebrity", "#GlowUp", "#Journey" },
            CTAs = new[]
            {
                "Which era do you prefer?",
                "Share this transformation!",
                "Tag someone who needs to see this!",
                "Comment your reaction!",
                "Follow for more celebrity transformations!"
            },
            TemplateIds = new[] { 14 }
        },
        ["nostalgia_90s"] = new CategoryInfo
        {
            Label = "90s Bollywood Nostalgia",
            PriorityWeight = 9,
            Keywords = new[] { "90s", "nostalgic", "classic", "retro", "iconic", "old bollywood", "90s movie" },
            HookTemplates = new[]
            {
                "This 90s Bollywood moment takes you back!",
                "Remember when {entity} ruled the screen?",
                "90s Bollywood hits different — {entity} edition!",
                "The golden era: {entity} in the 90s!",
                "This 90s {entity} clip is pure nostalgia!",
                "90s Bollywood was a different world — {entity} proves it!",
                "Take a trip back: {entity} 90s magic!",
                "This {entity} 90s moment is ICONIC!",
                "The 90s gave us {entity} — and we're still not over it!",
                "90s Bollywood nostalgia: {entity} edition!"
            },
            Hashtags = new[] { "#90sBollywood", "#Nostalgia", "#ClassicBollywood", "#Retro", "#Iconic", "#Bollywood" },
            CTAs = new[]
            {
                "Which 90s song is stuck in your head?",
                "Tag someone who loves 90s Bollywood!",
                "Share this with a 90s kid!",
                "Drop a ❤️ if you miss the 90s!",
                "Follow for more nostalgia!"
            },
            TemplateIds = new[] { 14 }
        },
        ["nostalgia_2000s"] = new CategoryInfo
        {
            Label = "2000s Bollywood Nostalgia",
            PriorityWeight = 9,
            Keywords = new[] { "2000s", "2000", "nostalgic", "early 2000s", "iconic", "retro" },
            HookTemplates = new[]
            {
                "This 2000s Bollywood moment takes you back!",
                "Remember when {entity} ruled the 2000s?",
                "2000s Bollywood hits different — {entity} edition!",
                "The early 2000s gave us {entity}!",
                "This 2000s {entity} clip is pure nostalgia!",
                "2000s Bollywood was a different world!",
                "Take a trip back: {entity} 2000s magic!",
                "This {entity} 2000s moment is ICONIC!",
                "The 2000s gave us {entity} — and we're still vibing!",
                "2000s Bollywood nostalgia: {entity} edition!"
            },
            Hashtags = new[] { "#2000sBollywood", "#Nostalgia", "#Early2000s", "#Classic", "#Bollywood", "#Retro" },
            CTAs = new[]
            {
                "Which 2000s song is your favorite?",
                "Tag someone who loves 2000s Bollywood!",
                "Share this with a 2000s kid!",
                "Drop a 🔥 if you miss the 2000s!",
                "Follow for more nostalgia!"
            },
            TemplateIds = new[] { 14 }
        },
        ["bollywood_facts"] = new CategoryInfo
        {
            Label = "Bollywood Unknown Facts",
            PriorityWeight = 9,
            Keywords = new[] { "facts", "unknown", "secrets", "trivia", "behind the scenes", "did you know" },
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
            Hashtags = new[] { "#BollywoodFacts", "#UnknownFacts", "#DidYouKnow", "#BollywoodTrivia", "#Secrets", "#FunFacts" },
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
        ["rejected_roles"] = new CategoryInfo
        {
            Label = "Rejected Roles",
            PriorityWeight = 9,
            Keywords = new[] { "rejected", "role", "casting", "what if", "famous role", "turned down" },
            HookTemplates = new[]
            {
                "You won't believe {entity} rejected this role!",
                "What if {entity} had taken this iconic role?",
                "This famous role was almost played by {entity}!",
                "{entity} turned down THIS role? 😱",
                "The rejected role that changed Bollywood forever!",
                "What {entity} almost played — you won't believe it!",
                "This casting decision shocked everyone!",
                "{entity} said NO to this blockbuster role!",
                "The role {entity} regrets turning down!",
                "Imagine {entity} in this role — alternate Bollywood!"
            },
            Hashtags = new[] { "#RejectedRoles", "#WhatIf", "#Bollywood", "#Casting", "#AlternateCasting", "#BollywoodFacts" },
            CTAs = new[]
            {
                "Which role do you wish they had taken?",
                "Share this with a Bollywood fan!",
                "Tag someone who needs to see this!",
                "What do you think — missed opportunity?",
                "Follow for more Bollywood secrets!"
            },
            TemplateIds = new[] { 14 }
        },
        ["alternate_casting"] = new CategoryInfo
        {
            Label = "Alternate Casting",
            PriorityWeight = 9,
            Keywords = new[] { "alternate casting", "what if", "different actor", "original cast", "imagine" },
            HookTemplates = new[]
            {
                "What if {entity} had been cast in this role?",
                "Imagine {entity} in this iconic role!",
                "This alternate casting would have changed everything!",
                "{entity} as {entity2}? The what-if scenario!",
                "The original casting plan that never happened!",
                "Alternate Bollywood: {entity} in a different role!",
                "What if {entity} had played THIS character?",
                "This dream casting almost happened!",
                "{entity} in an alternate Bollywood universe!",
                "The casting that would have broken the internet!"
            },
            Hashtags = new[] { "#AlternateCasting", "#WhatIf", "#Bollywood", "#DreamCasting", "#Imagine", "#MovieCasting" },
            CTAs = new[]
            {
                "Would you watch this alternate version?",
                "Share your dream casting!",
                "Tag someone who'd love this!",
                "What do you think — better or worse?",
                "Follow for more Bollywood what-ifs!"
            },
            TemplateIds = new[] { 14 }
        },
        ["bollywood_breakups"] = new CategoryInfo
        {
            Label = "Bollywood Breakups",
            PriorityWeight = 9,
            Keywords = new[] { "breakup", "split", "relationship", "dating", "love", "couple" },
            HookTemplates = new[]
            {
                "BREAKING: {entity} splits up — the real story!",
                "This {entity} breakup has everyone shocked!",
                "The truth behind {entity}'s breakup!",
                "{entity} and the end of a love story!",
                "This {entity} breakup is going VIRAL!",
                "What really happened between {entity}?",
                "{entity} breakup: Everything we know so far!",
                "The heartbreaking {entity} split!",
                "{entity}'s love life just took a turn!",
                "This {entity} breakup broke the internet!"
            },
            Hashtags = new[] { "#Breakup", "#Bollywood", "#Celebrity", "#Love", "#Relationship", "#Trending" },
            CTAs = new[]
            {
                "Are you shocked by this breakup?",
                "Share your thoughts below!",
                "Tag someone who needs to see this!",
                "What do you think really happened?",
                "Follow for more celebrity updates!"
            },
            TemplateIds = new[] { 14 }
        },
        ["bollywood_couples"] = new CategoryInfo
        {
            Label = "Bollywood Couples",
            PriorityWeight = 9,
            Keywords = new[] { "couple", "relationship", "love story", "wedding", "dating", "partners" },
            HookTemplates = new[]
            {
                "This {entity} couple is GOALS! 💕",
                "The cutest {entity} moment you'll see today!",
                "{entity} love story that gives us butterflies!",
                "This {entity} couple just made our day!",
                "{entity} spotted together — and they look amazing!",
                "The love story everyone is talking about: {entity}!",
                "{entity} couple goals for real! 😍",
                "This {entity} wedding photo is EVERYTHING!",
                "The most beautiful {entity} love story!",
                "{entity} just proved they're Bollywood's favorite couple!"
            },
            Hashtags = new[] { "#CoupleGoals", "#Bollywood", "#LoveStory", "#Wedding", "#CelebrityCouple", "#Romance" },
            CTAs = new[]
            {
                "Which Bollywood couple is your favorite?",
                "Share this with your partner!",
                "Tag someone who loves love stories!",
                "Drop a ❤️ if you're a fan!",
                "Follow for more couple content!"
            },
            TemplateIds = new[] { 14 }
        },
        ["bollywood_families"] = new CategoryInfo
        {
            Label = "Bollywood Families",
            PriorityWeight = 9,
            Keywords = new[] { "family", "kapoor", "bachchan", "khan", "dynasty", "connections", "relatives" },
            HookTemplates = new[]
            {
                "The {entity} family tree is INSANE!",
                "You won't believe these Bollywood family connections!",
                "{entity} family dynasty: The full story!",
                "This {entity} family photo is going viral!",
                "The powerful {entity} family of Bollywood!",
                "{entity} family connections will blow your mind!",
                "Inside the {entity} family's Bollywood empire!",
                "This {entity} family reunion photo is everything!",
                "The {entity} dynasty that rules Bollywood!",
                "Bollywood's most powerful family: {entity}!"
            },
            Hashtags = new[] { "#BollywoodFamily", "#Dynasty", "#Family", "#Bollywood", "#StarFamily", "#Connections" },
            CTAs = new[]
            {
                "Which Bollywood family is your favorite?",
                "Share this family connection!",
                "Tag someone who loves Bollywood families!",
                "Did you know these connections?",
                "Follow for more Bollywood family stories!"
            },
            TemplateIds = new[] { 14 }
        },
        ["star_kids"] = new CategoryInfo
        {
            Label = "Star Kids Then and Now",
            PriorityWeight = 9,
            Keywords = new[] { "star kid", "child", "nepotism", "debut", "career", "growing up" },
            HookTemplates = new[]
            {
                "Look how {entity}'s kid has grown! 🌟",
                "From child to star: {entity}'s kid's journey!",
                "This {entity} star kid transformation is incredible!",
                "{entity} kid is all grown up — and WOW!",
                "The {entity} star kid everyone is talking about!",
                "From nepotism to stardom: {entity} kid's path!",
                "This {entity} star kid debut is making headlines!",
                "{entity} child is following in their footsteps!",
                "The most adorable {entity} star kid photos!",
                "{entity} kid just made a powerful debut!"
            },
            Hashtags = new[] { "#StarKid", "#Bollywood", "#Nepotism", "#Debut", "#GrowingUp", "#NextGen" },
            CTAs = new[]
            {
                "Which star kid are you rooting for?",
                "Share this transformation!",
                "Tag someone who loves Bollywood families!",
                "What do you think — talent or nepotism?",
                "Follow for more star kid updates!"
            },
            TemplateIds = new[] { 14 }
        },
        ["celebrity_lifestyle"] = new CategoryInfo
        {
            Label = "Celebrity Lifestyle",
            PriorityWeight = 9,
            Keywords = new[] { "lifestyle", "luxury", "house", "car", "mansion", "expensive", "rich" },
            HookTemplates = new[]
            {
                "Inside {entity}'s luxurious lifestyle! 🏠",
                "Look at {entity}'s stunning mansion!",
                "This {entity} house tour is INCREDIBLE!",
                "{entity} lives like royalty — look at this!",
                "The jaw-dropping {entity} lifestyle!",
                "{entity}'s luxury car collection is insane!",
                "This {entity} home is a dream!",
                "{entity} really knows how to live!",
                "The expensive side of {entity}'s life!",
                "This {entity} lifestyle will make you jealous!"
            },
            Hashtags = new[] { "#LuxuryLifestyle", "#Celebrity", "#Bollywood", "#Mansion", "#Luxury", "#DreamHome" },
            CTAs = new[]
            {
                "Whose lifestyle do you admire most?",
                "Share this with a friend!",
                "Tag someone who loves luxury!",
                "What would you do with this lifestyle?",
                "Follow for more celebrity content!"
            },
            TemplateIds = new[] { 14 }
        },
        ["celebrity_networth"] = new CategoryInfo
        {
            Label = "Celebrity Net Worth",
            PriorityWeight = 9,
            Keywords = new[] { "net worth", "wealth", "rich", "earnings", "income", "salary", "worth" },
            HookTemplates = new[]
            {
                "You won't believe {entity}'s net worth! 💰",
                "How rich is {entity} really?",
                "This {entity} net worth number is SHOCKING!",
                "{entity}'s wealth will blow your mind!",
                "The real net worth of {entity}!",
                "{entity} earns THIS much?! 😱",
                "Inside {entity}'s massive fortune!",
                "This {entity} wealth report is jaw-dropping!",
                "{entity} is richer than you think!",
                "The shocking truth about {entity}'s net worth!"
            },
            Hashtags = new[] { "#NetWorth", "#Celebrity", "#Bollywood", "#Wealth", "#RichList", "#Earnings" },
            CTAs = new[]
            {
                "Were you surprised by this number?",
                "Share your reaction!",
                "Tag someone who needs to see this!",
                "Which celebrity's net worth shocked you?",
                "Follow for more celebrity facts!"
            },
            TemplateIds = new[] { 14 }
        },
        ["career_comparison"] = new CategoryInfo
        {
            Label = "Career Comparison",
            PriorityWeight = 9,
            Keywords = new[] { "career", "comparison", "achievements", "awards", "vs", "stats" },
            HookTemplates = new[]
            {
                "Career showdown: {entity} vs {entity2}!",
                "Who has the better career? {entity} or {entity2}?",
                "This {entity} vs {entity2} comparison is EPIC!",
                "The ultimate Bollywood career comparison!",
                "{entity} vs {entity2} — the numbers don't lie!",
                "Which career wins? {entity} or {entity2}!",
                "This {entity} career stat will shock you!",
                "{entity} vs {entity2}: Who's the real star?",
                "The Bollywood career battle: {entity} edition!",
                "Let's settle this: {entity} vs {entity2}!"
            },
            Hashtags = new[] { "#CareerComparison", "#Bollywood", "#Vs", "#Achievements", "#Awards", "#Stars" },
            CTAs = new[]
            {
                "Who wins this career battle?",
                "Share your verdict!",
                "Tag someone who'd argue about this!",
                "Which career impresses you more?",
                "Follow for more comparisons!"
            },
            TemplateIds = new[] { 14 }
        },
        ["movie_ending_explained"] = new CategoryInfo
        {
            Label = "Movie Ending Explained",
            PriorityWeight = 9,
            Keywords = new[] { "ending", "explained", "climax", "meaning", "spoiler", "theory" },
            HookTemplates = new[]
            {
                "{entity} ending explained — you won't believe what it means!",
                "This {entity} ending has everyone confused!",
                "The REAL meaning behind {entity}'s ending!",
                "{entity} climax: What actually happened?",
                "You missed THIS in {entity}'s ending!",
                "{entity} ending breakdown — everything explained!",
                "The hidden details in {entity}'s finale!",
                "This {entity} theory changes EVERYTHING!",
                "{entity} ending: What most people missed!",
                "The shocking truth behind {entity}'s ending!"
            },
            Hashtags = new[] { "#EndingExplained", "#Bollywood", "#Spoiler", "#MovieTheory", "#Climax", "#FilmAnalysis" },
            CTAs = new[]
            {
                "Did you catch this detail?",
                "Share your theory!",
                "Tag someone who needs to see this!",
                "What did you think of the ending?",
                "Follow for more movie breakdowns!"
            },
            TemplateIds = new[] { 14 }
        },
        ["biggest_flops"] = new CategoryInfo
        {
            Label = "Biggest Bollywood Flops",
            PriorityWeight = 9,
            Keywords = new[] { "flop", "disaster", "fail", "worst", "box office disaster", "bomb" },
            HookTemplates = new[]
            {
                "{entity} was the BIGGEST flop in Bollywood history!",
                "This {entity} disaster shocked everyone!",
                "How {entity} became a massive box office bomb!",
                "The real story behind {entity}'s failure!",
                "{entity} flopped HARD — here's why!",
                "This {entity} disaster is unbelievable!",
                "{entity} budget vs collection — the math is tragic!",
                "The most embarrassing {entity} failure!",
                "Why {entity} failed at the box office!",
                "{entity} is Bollywood's biggest disaster!"
            },
            Hashtags = new[] { "#Flop", "#Bollywood", "#BoxOfficeDisaster", "#Fail", "#WorstMovie", "#Disaster" },
            CTAs = new[]
            {
                "Did you watch this flop?",
                "Share why you think it failed!",
                "Tag someone who needs to see this!",
                "What went wrong with this movie?",
                "Follow for more Bollywood disasters!"
            },
            TemplateIds = new[] { 14 }
        },
        ["unexpected_blockbusters"] = new CategoryInfo
        {
            Label = "Unexpected Blockbusters",
            PriorityWeight = 9,
            Keywords = new[] { "blockbuster", "surprise hit", "sleeper hit", "unexpected", "low budget" },
            HookTemplates = new[]
            {
                "{entity} was an UNEXPECTED blockbuster!",
                "Nobody expected {entity} to be this huge!",
                "The surprise hit that broke all records: {entity}!",
                "{entity} went from sleeper hit to MEGA blockbuster!",
                "This {entity} success story is INSPIRING!",
                "How {entity} became an overnight sensation!",
                "{entity} surprised EVERYONE at the box office!",
                "The low-budget blockbuster: {entity}!",
                "{entity}'s success no one saw coming!",
                "From unknown to blockbuster: {entity}'s incredible journey!"
            },
            Hashtags = new[] { "#Blockbuster", "#SurpriseHit", "#Bollywood", "#SleeperHit", "#Success", "#BoxOffice" },
            CTAs = new[]
            {
                "Did you watch this surprise hit?",
                "Share this success story!",
                "Tag someone who needs to see this!",
                "What made this movie so special?",
                "Follow for more Bollywood stories!"
            },
            TemplateIds = new[] { 14 }
        },
        ["iconic_songs"] = new CategoryInfo
        {
            Label = "Iconic Bollywood Songs",
            PriorityWeight = 9,
            Keywords = new[] { "song", "music", "iconic", "hit", "melody", "classic", "legendary" },
            HookTemplates = new[]
            {
                "This {entity} song is ICONIC! 🎵",
                "The {entity} melody that lives in our hearts!",
                "{entity} song that defined a generation!",
                "This {entity} track is EVERYTHING! 🎶",
                "The legendary {entity} song you can't forget!",
                "{entity} music that still gives goosebumps!",
                "This {entity} hit is timeless!",
                "{entity} song = instant nostalgia! 🎵",
                "The {entity} melody that broke the internet!",
                "This iconic {entity} song deserves more love!"
            },
            Hashtags = new[] { "#IconicSong", "#Bollywood", "#Music", "#Melody", "#Classic", "#BollywoodMusic" },
            CTAs = new[]
            {
                "Which Bollywood song is your all-time favorite?",
                "Share this with a music lover!",
                "Tag someone who loves this song!",
                "Drop your favorite Bollywood song in comments!",
                "Follow for more iconic music!"
            },
            TemplateIds = new[] { 14 }
        },
        ["movie_bts"] = new CategoryInfo
        {
            Label = "Movie Behind The Scenes",
            PriorityWeight = 9,
            Keywords = new[] { "behind the scenes", "making", "production", "shoot", "bts", "making of" },
            HookTemplates = new[]
            {
                "Behind the scenes of {entity} — you NEED to see this!",
                "The making of {entity} is INSANE!",
                "This {entity} BTS footage is gold!",
                "{entity} behind the scenes — the real magic!",
                "You won't believe how {entity} was made!",
                "The hidden world behind {entity}! 🎬",
                "{entity} BTS: The story they didn't show!",
                "This {entity} making-of clip is incredible!",
                "Behind every scene of {entity} — pure effort!",
                "{entity} production secrets revealed! 🎥"
            },
            Hashtags = new[] { "#BTS", "#BehindTheScenes", "#Bollywood", "#MakingOf", "#FilmProduction", "#MovieMagic" },
            CTAs = new[]
            {
                "Did you know about this BTS?",
                "Share this behind-the-scenes!",
                "Tag someone who loves movie making!",
                "What surprised you most?",
                "Follow for more BTS content!"
            },
            TemplateIds = new[] { 14 }
        },
        ["upcoming_movies"] = new CategoryInfo
        {
            Label = "Upcoming Movies",
            PriorityWeight = 9,
            Keywords = new[] { "upcoming", "release", "next", "预告", "preview", "2026", "announcement" },
            HookTemplates = new[]
            {
                "Coming soon: {entity} — get ready! 🎬",
                "The most anticipated {entity} is almost here!",
                "{entity} release date CONFIRMED!",
                "This {entity} is going to be EPIC!",
                "Get ready for {entity} — 2026's biggest movie!",
                "{entity} is coming and we can't wait!",
                "The countdown to {entity} begins NOW!",
                "{entity} preview just dropped — it's INSANE!",
                "This {entity} is going to break the internet!",
                "{entity} is the movie everyone's waiting for!"
            },
            Hashtags = new[] { "#UpcomingMovie", "#Bollywood", "#ComingSoon", "#NewMovie", "#2026", "#Anticipation" },
            CTAs = new[]
            {
                "Are you excited for this movie?",
                "Share this with a movie fan!",
                "Tag someone who needs to see this!",
                "Which upcoming movie are you most excited for?",
                "Follow for more movie updates!"
            },
            TemplateIds = new[] { 14 }
        },

        // ═══════════════════════════════════════════════════════════════
        // POLITICS CATEGORIES (26–50)
        // ═══════════════════════════════════════════════════════════════
        ["political_breaking"] = new CategoryInfo
        {
            Label = "Political Breaking News",
            PriorityWeight = 9,
            Keywords = new[] { "political", "breaking", "news", "government", "party", "leader", "statement" },
            HookTemplates = new[]
            {
                "BREAKING: Major political development right now!",
                "This just happened in politics — you need to know!",
                "Nobody saw this coming: {entity} makes huge move!",
                "Just in: {entity} announces major decision!",
                "Political world is SHOCKED right now!",
                "ALERT: Big political news from {entity}!",
                "The truth about {entity} — what's really happening!",
                "What {entity} did next left everyone shocked!",
                "This political update changes everything!",
                "Major headline: {entity} just confirmed..."
            },
            Hashtags = new[] { "#PoliticalBreaking", "#BreakingNews", "#Politics", "#India", "#PoliticalNews", "#Trending" },
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
        ["politics_explained"] = new CategoryInfo
        {
            Label = "Indian Politics Explained",
            PriorityWeight = 9,
            Keywords = new[] { "politics", "explained", "issue", "policy", "analysis", "simple" },
            HookTemplates = new[]
            {
                "Indian politics EXPLAINED in simple terms!",
                "This political issue in 60 seconds!",
                "What's really happening in Indian politics?",
                "Breaking down the {entity} issue simply!",
                "Politics explained like never before!",
                "The {entity} issue — what you need to know!",
                "This political analysis will open your eyes!",
                "{entity} explained: The full picture!",
                "Indian politics made SIMPLE — {entity} edition!",
                "The {entity} story everyone is confused about!"
            },
            Hashtags = new[] { "#PoliticsExplained", "#IndianPolitics", "#Policy", "#Analysis", "#Simple", "#Understanding" },
            CTAs = new[]
            {
                "Did this help you understand better?",
                "Share this explanation!",
                "Tag someone who needs to see this!",
                "What other political issue should we explain?",
                "Follow for more political explainers!"
            },
            TemplateIds = new[] { 14 }
        },
        ["parliament_update"] = new CategoryInfo
        {
            Label = "Parliament Update",
            PriorityWeight = 9,
            Keywords = new[] { "parliament", "session", "debate", "question hour", "Lok Sabha", "Rajya Sabha" },
            HookTemplates = new[]
            {
                "Parliament update: {entity} just happened!",
                "Lok Sabha is BUZZING right now!",
                "This parliament session is INTENSE!",
                "{entity} just dropped a bombshell in Parliament!",
                "Parliament debate heating up: {entity}!",
                "Rajya Sabha just witnessed THIS!",
                "The most dramatic parliament moment today!",
                "{entity} in Parliament — what just happened!",
                "Parliament session update: Major development!",
                "This parliament debate is going viral!"
            },
            Hashtags = new[] { "#Parliament", "#LokSabha", "#RajyaSabha", "#IndianPolitics", "#Debate", "#Session" },
            CTAs = new[]
            {
                "What's your take on this debate?",
                "Share your thoughts!",
                "Tag someone who follows politics!",
                "What should Parliament focus on next?",
                "Follow for more parliament updates!"
            },
            TemplateIds = new[] { 14 }
        },
        ["election_watch"] = new CategoryInfo
        {
            Label = "Election Watch",
            PriorityWeight = 9,
            Keywords = new[] { "election", "vote", "campaign", "candidate", "polling", "result" },
            HookTemplates = new[]
            {
                "Election ALERT: {entity} just made a move!",
                "This election update is HUGE!",
                "{entity} campaign is going viral!",
                "Election watch: {entity} dominates!",
                "This election result is shocking!",
                "{entity} just changed the election game!",
                "Election campaign update: {entity}!",
                "The {entity} election story everyone is watching!",
                "This {entity} polling data is incredible!",
                "Election buzz: {entity} takes the lead!"
            },
            Hashtags = new[] { "#Election", "#Vote", "#Campaign", "#Politics", "#India", "#ElectionWatch" },
            CTAs = new[]
            {
                "Are you following this election?",
                "Share your election prediction!",
                "Tag someone who needs to see this!",
                "What issues matter most to you?",
                "Follow for more election updates!"
            },
            TemplateIds = new[] { 14 }
        },
        ["politician_statement"] = new CategoryInfo
        {
            Label = "Politician Statement",
            PriorityWeight = 9,
            Keywords = new[] { "statement", "speech", "quote", "leader", "minister", "response" },
            HookTemplates = new[]
            {
                "This {entity} statement is going VIRAL!",
                "{entity} just made a HUGE statement!",
                "You won't believe what {entity} just said!",
                "This {entity} speech has everyone talking!",
                "{entity} dropped a bombshell statement!",
                "The {entity} quote everyone is sharing!",
                "This {entity} response is powerful!",
                "{entity} just made HEADLINES with this!",
                "This {entity} speech is a MUST-WATCH!",
                "{entity} statement that broke the internet!"
            },
            Hashtags = new[] { "#Statement", "#Politician", "#Politics", "#Speech", "#India", "#Viral" },
            CTAs = new[]
            {
                "What's your take on this statement?",
                "Do you agree with {entity}?",
                "Share this statement!",
                "Tag someone who needs to see this!",
                "Follow for more political updates!"
            },
            TemplateIds = new[] { 14 }
        },
        ["political_faceoff"] = new CategoryInfo
        {
            Label = "Political Face-Off",
            PriorityWeight = 9,
            Keywords = new[] { "face off", "debate", "vs", "argument", "clash", "confrontation" },
            HookTemplates = new[]
            {
                "EPIC political face-off: {entity} vs {entity2}!",
                "This political clash is INTENSE!",
                "{entity} just challenged {entity2}!",
                "The political showdown everyone is watching!",
                "This {entity} vs {entity2} debate is FIRE!",
                "Political clash: {entity} strikes back!",
                "This face-off has the internet buzzing!",
                "{entity} vs {entity2} — who wins?",
                "The most heated political argument today!",
                "This political confrontation is UNREAL!"
            },
            Hashtags = new[] { "#FaceOff", "#PoliticalDebate", "#Politics", "#Clash", "#India", "#Argument" },
            CTAs = new[]
            {
                "Who won this face-off?",
                "Share your opinion!",
                "Tag someone who'd love this debate!",
                "What's your take on this clash?",
                "Follow for more political drama!"
            },
            TemplateIds = new[] { 14 }
        },
        ["government_rule"] = new CategoryInfo
        {
            Label = "New Government Rule",
            PriorityWeight = 9,
            Keywords = new[] { "rule", "law", "regulation", "policy", "order", "guideline", "decision" },
            HookTemplates = new[]
            {
                "NEW government rule just announced! 📢",
                "This new law affects EVERYONE!",
                "{entity} just introduced a major rule change!",
                "Government rule ALERT: What you need to know!",
                "This new regulation will change everything!",
                "{entity} just dropped a new policy!",
                "New government order: Important update!",
                "This law change is HUGE — {entity}!",
                "Government just announced THIS new rule!",
                "The new regulation everyone is talking about!"
            },
            Hashtags = new[] { "#GovernmentRule", "#NewLaw", "#Policy", "#India", "#Regulation", "#Update" },
            CTAs = new[]
            {
                "How does this rule affect you?",
                "Share this with someone who needs to know!",
                "Tag someone who needs to see this!",
                "What do you think of this rule?",
                "Follow for more government updates!"
            },
            TemplateIds = new[] { 14 }
        },
        ["budget_economy"] = new CategoryInfo
        {
            Label = "Budget and Economy",
            PriorityWeight = 9,
            Keywords = new[] { "budget", "economy", "tax", "inflation", "GDP", "financial", "rupee" },
            HookTemplates = new[]
            {
                "Budget ALERT: {entity} just dropped a bombshell!",
                "This economic update changes everything!",
                "{entity} budget numbers are IN!",
                "The truth about India's economy — {entity}!",
                "This budget breakdown you NEED to see!",
                "{entity} just made a massive economic move!",
                "Budget update: What it means for YOU!",
                "This economic data is SHOCKING!",
                "{entity} just revealed the budget numbers!",
                "The economy is changing — here's the {entity} update!"
            },
            Hashtags = new[] { "#Budget", "#Economy", "#Tax", "#India", "#Finance", "#Inflation" },
            CTAs = new[]
            {
                "How does this budget affect you?",
                "Share your budget reaction!",
                "Tag someone who needs to see this!",
                "What do you think of the economy?",
                "Follow for more budget updates!"
            },
            TemplateIds = new[] { 14 }
        },
        ["government_data"] = new CategoryInfo
        {
            Label = "Government Data",
            PriorityWeight = 9,
            Keywords = new[] { "data", "statistics", "report", "numbers", "survey", "index", "ranking" },
            HookTemplates = new[]
            {
                "Government data just dropped — and it's SHOCKING!",
                "This {entity} report has everyone talking!",
                "The numbers are IN: {entity} data revealed!",
                "This government statistic will blow your mind!",
                "{entity} just released crucial data!",
                "New government report: {entity} edition!",
                "This data changes everything we thought we knew!",
                "{entity} statistics are jaw-dropping!",
                "The government just shared THIS data!",
                "This {entity} index is making headlines!"
            },
            Hashtags = new[] { "#Data", "#Statistics", "#Government", "#India", "#Report", "#Numbers" },
            CTAs = new[]
            {
                "Were you surprised by these numbers?",
                "Share this data with someone!",
                "Tag someone who needs to see this!",
                "What does this data mean to you?",
                "Follow for more government data!"
            },
            TemplateIds = new[] { 14 }
        },
        ["india_world"] = new CategoryInfo
        {
            Label = "India World News",
            PriorityWeight = 9,
            Keywords = new[] { "international", "global", "world", "india", "foreign", "diplomacy" },
            HookTemplates = new[]
            {
                "BREAKING: India makes global headlines!",
                "This international development involves India!",
                "{entity} just changed India's global standing!",
                "World is watching India right now!",
                "This global news impacts India directly!",
                "{entity} just made a major diplomatic move!",
                "India vs the world — the latest development!",
                "This international {entity} story is HUGE!",
                "Global alert: {entity} and India!",
                "The world is talking about India — {entity}!"
            },
            Hashtags = new[] { "#IndiaWorld", "#International", "#Global", "#Diplomacy", "#India", "#WorldNews" },
            CTAs = new[]
            {
                "What's your take on this global issue?",
                "Share this with someone!",
                "Tag someone who follows world news!",
                "How do you see India's role?",
                "Follow for more world news!"
            },
            TemplateIds = new[] { 14 }
        },
        ["india_vs_world"] = new CategoryInfo
        {
            Label = "India vs World",
            PriorityWeight = 9,
            Keywords = new[] { "india vs", "comparison", "world", "global", "ranking", "data" },
            HookTemplates = new[]
            {
                "India vs the world — who's winning?",
                "This comparison is MIND-BLOWING!",
                "India dominates the world in THIS!",
                "How India compares to the world — {entity}!",
                "India vs {entity} — the numbers don't lie!",
                "This India ranking is INSANE!",
                "India just beat the world at {entity}!",
                "The India vs world comparison you NEED to see!",
                "India leads the world in THIS!",
                "India vs the world — {entity} edition!"
            },
            Hashtags = new[] { "#IndiaVsWorld", "#Comparison", "#Ranking", "#India", "#Global", "#Data" },
            CTAs = new[]
            {
                "Did you know India leads in this?",
                "Share this comparison!",
                "Tag someone who needs to see this!",
                "What other comparisons do you want?",
                "Follow for more India updates!"
            },
            TemplateIds = new[] { 14 }
        },
        ["politician_profile"] = new CategoryInfo
        {
            Label = "Politician Profile",
            PriorityWeight = 9,
            Keywords = new[] { "leader", "profile", "career", "background", "journey", "biography" },
            HookTemplates = new[]
            {
                "The rise of {entity} — a political journey!",
                "This {entity} profile will blow your mind!",
                "From nobody to power: {entity}'s incredible story!",
                "Inside {entity}'s political career!",
                "The {entity} story you never knew!",
                "How {entity} became one of India's most powerful leaders!",
                "The untold journey of {entity}!",
                "{entity} profile: Everything you need to know!",
                "The making of {entity} — a political biography!",
                "This {entity} story is INCREDIBLE!"
            },
            Hashtags = new[] { "#PoliticianProfile", "#Leader", "#Politics", "#India", "#Journey", "#Biography" },
            CTAs = new[]
            {
                "What do you think of this leader?",
                "Share this profile!",
                "Tag someone who needs to see this!",
                "Which politician's journey inspires you?",
                "Follow for more leader profiles!"
            },
            TemplateIds = new[] { 14 }
        },
        ["political_flashback"] = new CategoryInfo
        {
            Label = "Political Flashback",
            PriorityWeight = 9,
            Keywords = new[] { "flashback", "history", "past", "memory", "anniversary", "on this day" },
            HookTemplates = new[]
            {
                "Political flashback: This day in history!",
                "Remember when {entity} made THIS move?",
                "On this day: {entity} changed politics forever!",
                "This historical {entity} moment is ICONIC!",
                "Flashback to the day {entity} shocked the nation!",
                "The {entity} moment that made history!",
                "On this day: The biggest political event!",
                "This {entity} flashback gives us goosebumps!",
                "History remembers: {entity} on this day!",
                "The political anniversary everyone is celebrating!"
            },
            Hashtags = new[] { "#Flashback", "#History", "#OnThisDay", "#Politics", "#India", "#Anniversary" },
            CTAs = new[]
            {
                "Do you remember this moment?",
                "Share this flashback!",
                "Tag someone who needs to see this!",
                "What other historical moment should we cover?",
                "Follow for more political history!"
            },
            TemplateIds = new[] { 14 }
        },
        ["myth_vs_fact"] = new CategoryInfo
        {
            Label = "Political Myth vs Fact",
            PriorityWeight = 9,
            Keywords = new[] { "myth", "fact", "claim", "check", "fake", "truth", "debunked" },
            HookTemplates = new[]
            {
                "MYTH vs FACT: {entity} edition!",
                "This claim about {entity} is FAKE — here's the truth!",
                "Debunking the biggest {entity} myth!",
                "The truth about {entity} — myth BUSTED!",
                "This {entity} claim is MISLEADING — fact check!",
                "MYTH BUSTED: The real story behind {entity}!",
                "You believed this about {entity} — but it's FALSE!",
                "Fact check: {entity} — what's really true?",
                "The {entity} myth that fooled everyone!",
                "This viral {entity} claim is completely FALSE!"
            },
            Hashtags = new[] { "#MythVsFact", "#FactCheck", "#Truth", "#Politics", "#Debunked", "#India" },
            CTAs = new[]
            {
                "Did you know this was fake?",
                "Share this fact check!",
                "Tag someone who needs to see this!",
                "What other myths should we bust?",
                "Follow for more fact checks!"
            },
            TemplateIds = new[] { 14 }
        },
        ["what_actually_happened"] = new CategoryInfo
        {
            Label = "What Actually Happened",
            PriorityWeight = 9,
            Keywords = new[] { "actually happened", "timeline", "story", "incident", "event", "explained" },
            HookTemplates = new[]
            {
                "What actually happened with {entity}?",
                "The FULL timeline of {entity} — explained!",
                "Here's the complete story behind {entity}!",
                "{entity} incident: What REALLY went down!",
                "Everything that happened with {entity} — in order!",
                "The untold story of {entity}'s incident!",
                "{entity} event breakdown — the full picture!",
                "What actually happened: {entity} edition!",
                "The real timeline of {entity} — no fake news!",
                "This {entity} story is WILDER than you think!"
            },
            Hashtags = new[] { "#WhatHappened", "#Timeline", "#Explained", "#Politics", "#India", "#Story" },
            CTAs = new[]
            {
                "Did you know the full story?",
                "Share this timeline!",
                "Tag someone who needs to see this!",
                "What do you think actually happened?",
                "Follow for more event breakdowns!"
            },
            TemplateIds = new[] { 14 }
        },
        ["public_reaction"] = new CategoryInfo
        {
            Label = "Public Reaction",
            PriorityWeight = 9,
            Keywords = new[] { "reaction", "response", "opinion", "viral", "social media", "twitter" },
            HookTemplates = new[]
            {
                "Twitter is MELTING DOWN over {entity}!",
                "The public reaction to {entity} is INSANE!",
                "This {entity} response is going VIRAL!",
                "Everyone is talking about {entity} right now!",
                "The internet's reaction to {entity} is priceless!",
                "{entity} just broke social media!",
                "This public reaction to {entity} is EVERYTHING!",
                "Twitter炸了 over {entity}!",
                "The viral response to {entity} you NEED to see!",
                "People are GOING OFF about {entity}!"
            },
            Hashtags = new[] { "#PublicReaction", "#Viral", "#Twitter", "#SocialMedia", "#Trending", "#Reaction" },
            CTAs = new[]
            {
                "What's YOUR reaction to this?",
                "Share this with someone!",
                "Tag someone who needs to see this!",
                "Drop your reaction in the comments!",
                "Follow for more viral reactions!"
            },
            TemplateIds = new[] { 14 }
        },
        ["five_things_today"] = new CategoryInfo
        {
            Label = "5 Things Today",
            PriorityWeight = 9,
            Keywords = new[] { "top 5", "today", "biggest", "important", "summary", "roundup" },
            HookTemplates = new[]
            {
                "5 things you NEED to know today!",
                "Top 5 stories of the day — {entity} edition!",
                "Today's BIGGEST stories in 60 seconds!",
                "5 things that matter today!",
                "The 5 most important stories right now!",
                "Today's roundup: 5 stories you can't miss!",
                "5 things happening in India today!",
                "The top 5 headlines you need to know!",
                "5 stories that shook the world today!",
                "Today's essential 5 — {entity} leads!"
            },
            Hashtags = new[] { "#Top5", "#Today", "#Roundup", "#Headlines", "#India", "#Summary" },
            CTAs = new[]
            {
                "Which story surprised you most?",
                "Share this roundup!",
                "Tag someone who needs to see this!",
                "What other stories should we cover?",
                "Follow for daily roundups!"
            },
            TemplateIds = new[] { 14 }
        },
        ["fact_check"] = new CategoryInfo
        {
            Label = "Fact Check",
            PriorityWeight = 9,
            Keywords = new[] { "fact check", "true", "false", "misleading", "verified", "claim" },
            HookTemplates = new[]
            {
                "FACT CHECK: Is this {entity} claim TRUE?",
                "This viral claim about {entity} — TRUE or FALSE?",
                "We checked: {entity} claim is MISLEADING!",
                "The truth behind this {entity} claim!",
                "Fact check: {entity} — verified or fake?",
                "This {entity} claim is FALSE — here's proof!",
                "VERIFIED: The real story behind {entity}!",
                "Fact check time: {entity} edition!",
                "This viral {entity} post is MISLEADING!",
                "The {entity} fact check you NEED to see!"
            },
            Hashtags = new[] { "#FactCheck", "#TrueOrFalse", "#Verified", "#Politics", "#India", "#Truth" },
            CTAs = new[]
            {
                "Did you think this was true?",
                "Share this fact check!",
                "Tag someone who needs to see this!",
                "What other claims should we fact-check?",
                "Follow for more fact checks!"
            },
            TemplateIds = new[] { 14 }
        },
        ["what_happens_next"] = new CategoryInfo
        {
            Label = "What Happens Next",
            PriorityWeight = 9,
            Keywords = new[] { "what happens next", "prediction", "future", "outcome", "next step" },
            HookTemplates = new[]
            {
                "What happens next with {entity}?",
                "The future of {entity} — what to expect!",
                "{entity} just changed — what's next?",
                "The next chapter for {entity}!",
                "What comes next for {entity}?",
                "The prediction everyone is making about {entity}!",
                "This {entity} story is FAR from over!",
                "Next step: {entity} takes a new direction!",
                "What happens next? {entity} edition!",
                "The future outlook for {entity} — analysis!"
            },
            Hashtags = new[] { "#WhatHappensNext", "#Prediction", "#Future", "#Politics", "#India", "#WhatNext" },
            CTAs = new[]
            {
                "What do you think happens next?",
                "Share your prediction!",
                "Tag someone who needs to see this!",
                "What's your take on the future?",
                "Follow for more predictions!"
            },
            TemplateIds = new[] { 14 }
        },
        ["bollywood_teasers"] = new CategoryInfo
        {
            Label = "Bollywood Teasers",
            PriorityWeight = 9,
            Keywords = new[] { "teaser", "first look", "glimpse", "preview", "reveal" },
            HookTemplates = new[]
            {
                "🎬 {entity} teaser just dropped!",
                "Watch this epic {entity} teaser!",
                "This {entity} looks INSANE!",
                "{entity} official teaser is here!",
                "You NEED to see this {entity} teaser!",
                "This {entity} moment is iconic!",
                "Viral alert: {entity} teaser!",
                "This {entity} is going to be HUGE!",
                "Can't wait for {entity}!",
                "{entity} just set the internet on fire!"
            },
            Hashtags = new[] { "#BollywoodTeaser", "#MovieTeaser", "#Bollywood", "#ViralReels", "#NewMovie", "#Reels" },
            CTAs = new[]
            {
                "Watch till the end!",
                "Tag someone who loves {entity}!",
                "Share this reel!",
                "Comment your excitement!",
                "Follow for more such teasers!"
            },
            TemplateIds = new[] { 14 }
        },
        ["bollywood_trailers"] = new CategoryInfo
        {
            Label = "Bollywood Trailers",
            PriorityWeight = 9,
            Keywords = new[] { "trailer", "official", "launch", "preview", "upcoming" },
            HookTemplates = new[]
            {
                "🎥 {entity} trailer is OUT!",
                "This {entity} trailer is EPIC!",
                "{entity} official trailer just dropped!",
                "Watch this {entity} trailer NOW!",
                "This {entity} is going to be a BLOCKBUSTER!",
                "{entity} trailer broke the internet!",
                "You CAN'T miss this {entity} trailer!",
                "This {entity} looks like a HIT!",
                "{entity} trailer is trending #1!",
                "Blockbuster alert: {entity} trailer!"
            },
            Hashtags = new[] { "#BollywoodTrailer", "#MovieTrailer", "#Bollywood", "#Trailer", "#NewMovie", "#Reels" },
            CTAs = new[]
            {
                "Are you excited for {entity}?",
                "Tag someone who needs to watch this!",
                "Share this trailer!",
                "Comment your expectations!",
                "Follow for more trailers!"
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
        ["reels_bollywood_teasers"] = new SeriesInfo { Name = "Bollywood Teasers", BestFor = new[] { "bollywood_teasers" }, Description = "Latest Bollywood teasers" },
        ["reels_bollywood_trailers"] = new SeriesInfo { Name = "Bollywood Trailers", BestFor = new[] { "bollywood_trailers" }, Description = "Latest Bollywood trailers" },
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