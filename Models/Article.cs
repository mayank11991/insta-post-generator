namespace InstaPostGenerator.Models;

public class Article
{
    public string Title { get; set; } = "";
    public string Link { get; set; } = "";
    public string Thumbnail { get; set; } = "";
    public SourceInfo Source { get; set; } = new();
    public string Summary { get; set; } = "";
    public string IsoDate { get; set; } = "";
    public string Category { get; set; } = "";
    public int RelatedSources { get; set; } = 1;
}

public class SourceInfo
{
    public string Name { get; set; } = "";
}

public class ProcessedArticle
{
    public Article Article { get; set; } = new();
    public string Category { get; set; } = "";
    public string CategoryLabel { get; set; } = "";
    public double Confidence { get; set; }
    public int Priority { get; set; }
    public string Hook { get; set; } = "";
    public string CTA { get; set; } = "";
    public string Series { get; set; } = "";
    public string SeriesName { get; set; } = "";
    public List<string> Hashtags { get; set; } = new();
    public int[] TemplateIds { get; set; } = new[] { 1, 2, 3, 4 };
    public bool QualityPassed { get; set; }
    public List<string> QualityIssues { get; set; } = new();
    public Entities Entities { get; set; } = new();
    public string ImagePath { get; set; } = "";
    public string Caption { get; set; } = "";
}

public class Entities
{
    public List<string> Celebrities { get; set; } = new();
    public List<string> Movies { get; set; } = new();
    public string Primary { get; set; } = "";
}

public class SeenStore
{
    public HashSet<string> Ids { get; set; } = new();
    public HashSet<string> Titles { get; set; } = new();
}

public class ContentMixData
{
    public List<string> RecentCategories { get; set; } = new();
    public List<string> RecentSeries { get; set; } = new();
    public List<string> RecentCTAs { get; set; } = new();
}

public class PostDisplayItem
{
    public int Index { get; set; }
    public string ImagePath { get; set; } = "";
    public string Caption { get; set; } = "";
    public string SourceUrl { get; set; } = "";
    public string Hashtags { get; set; } = "";
    public string CategoryLabel { get; set; } = "";
    public string Hook { get; set; } = "";
    public string SeriesName { get; set; } = "";
}