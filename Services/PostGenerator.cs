using SkiaSharp;
using InstaPostGenerator.Models;
using System.Text.RegularExpressions;
using System.IO;

namespace InstaPostGenerator.Services;

public static class PostGenerator
{
    private static readonly Random _random = new();
    private static readonly Regex DevanagariRegex = new(@"[\u0900-\u097F]");

    // Font name constants
    private const string FONT_LEAGUE_SPARTAN = "LeagueSpartan-Bold.otf";
    private const string FONT_ARENA = "Arena-rvwaK.ttf";

    // Helper for SkiaSharp 3.x - SKFont has no GetMetrics, use SKPaint instead
    private static SKFontMetrics GetMetrics(this SKFont font)
    {
        using var paint = new SKPaint { TextSize = font.Size, Typeface = font.Typeface, IsAntialias = true };
        paint.GetFontMetrics(out var metrics);
        return metrics;
    }

    public static async Task GenerateTestImageAsync(string outputPath)
    {
        var bitmap = new SKBitmap(Config.EXPORT_WIDTH, Config.EXPORT_HEIGHT);
        using var canvas = new SKCanvas(bitmap);

        // White background
        canvas.Clear(new SKColor(255, 255, 255));

        // Download a test image for background
        SKBitmap testImage = null;
        try
        {
            using var httpClient = new HttpClient();
            httpClient.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0");
            var imageBytes = await httpClient.GetByteArrayAsync("https://picsum.photos/1080/1350");
            testImage = SKBitmap.Decode(imageBytes);
        }
        catch { }

        // Draw test image - COVER MODE (fill entire canvas)
        if (testImage != null)
        {
            var scale = Math.Max((float)Config.EXPORT_WIDTH / testImage.Width, (float)Config.EXPORT_HEIGHT / testImage.Height);
            var newW = Math.Max(1, (int)(testImage.Width * scale));
            var newH = Math.Max(1, (int)(testImage.Height * scale));
            var fitted = testImage.Resize(new SKImageInfo(newW, newH), SKSamplingOptions.Default);
            var offsetX = (Config.EXPORT_WIDTH - newW) / 2;
            var offsetY = (Config.EXPORT_HEIGHT - newH) / 2;
            canvas.DrawBitmap(fitted, offsetX, offsetY);
        }

        var headingFont = CreateFont(FONT_LEAGUE_SPARTAN, Config.EXPORT_WIDTH * 0.055f, SKFontStyleWeight.Bold);
        var sourceFont = CreateFont(FONT_LEAGUE_SPARTAN, Config.EXPORT_WIDTH * 0.025f, SKFontStyleWeight.Bold);
        var timestampFont = CreateFont(FONT_LEAGUE_SPARTAN, Config.EXPORT_WIDTH * 0.018f, SKFontStyleWeight.Normal);
        var brandFont = CreateFont(FONT_LEAGUE_SPARTAN, Config.EXPORT_WIDTH * 0.042f, SKFontStyleWeight.Bold);

        var templateArgs = new TemplateArgs
        {
            Canvas = canvas,
            Article = null,
            Title = "Breaking News: This is a Test Title for the Custom Template Layout",
            SourceName = "TEST SOURCE",
            HeadingFont = headingFont,
            SourceFont = sourceFont,
            BrandFont = brandFont,
            TimestampFont = timestampFont,
            Pad = Config.EXPORT_WIDTH * 0.04f,
            CornerRadius = Config.EXPORT_WIDTH * 0.045f,
            ImageWidth = Config.EXPORT_WIDTH,
            ImageHeight = Config.EXPORT_HEIGHT
        };

        await CreateCustomTemplateAsync(templateArgs);

        // Save
        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        using var stream = File.OpenWrite(outputPath);
        data.SaveTo(stream);
    }

    public static async Task<string> CreateNewsImageAsync(ProcessedArticle processedArticle, string outputPath, int template = 0, int[] templateIds = null)
    {
        var article = processedArticle.Article;
        
        // Download article image for background
        SKBitmap articleBitmap = null;
        try
        {
            if (!string.IsNullOrEmpty(article.Thumbnail))
            {
                using var httpClient = new HttpClient();
                httpClient.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/537.36");
                var imageBytes = await httpClient.GetByteArrayAsync(article.Thumbnail);
                articleBitmap = SKBitmap.Decode(imageBytes);
            }
        }
        catch
        {
            articleBitmap = null;
        }

        var title = (article.Title ?? "").Trim();
        if (string.IsNullOrEmpty(title))
            title = "No title available";
        title = Regex.Replace(title, @"\s+", " ");

        var displayTitle = title;

        // Create canvas with custom template
        var canvasBitmap = new SKBitmap(Config.EXPORT_WIDTH, Config.EXPORT_HEIGHT);
        using (var canvas = new SKCanvas(canvasBitmap))
        {
            // White background fallback
            canvas.Clear(new SKColor(255, 255, 255));
            
            // Draw article image - COVER MODE (fill entire canvas)
            if (articleBitmap != null)
            {
                var scale = Math.Max((float)Config.EXPORT_WIDTH / articleBitmap.Width, (float)Config.EXPORT_HEIGHT / articleBitmap.Height);
                var newW = Math.Max(1, (int)(articleBitmap.Width * scale));
                var newH = Math.Max(1, (int)(articleBitmap.Height * scale));
                
                var fitted = articleBitmap.Resize(new SKImageInfo(newW, newH), SKSamplingOptions.Default);
                var offsetX = (Config.EXPORT_WIDTH - newW) / 2;
                var offsetY = (Config.EXPORT_HEIGHT - newH) / 2;
                canvas.DrawBitmap(fitted, offsetX, offsetY);
            }
        }

        using var drawCanvas = new SKCanvas(canvasBitmap);

        // Draw custom template
        var headingFont = CreateFont(FONT_LEAGUE_SPARTAN, Config.EXPORT_WIDTH * 0.055f, SKFontStyleWeight.Bold);
        var sourceFont = CreateFont(FONT_LEAGUE_SPARTAN, Config.EXPORT_WIDTH * 0.025f, SKFontStyleWeight.Bold);
        var timestampFont = CreateFont(FONT_LEAGUE_SPARTAN, Config.EXPORT_WIDTH * 0.018f, SKFontStyleWeight.Normal);
        var brandFont = CreateFont(FONT_LEAGUE_SPARTAN, Config.EXPORT_WIDTH * 0.042f, SKFontStyleWeight.Bold);

        var sourceName = (article.Source?.Name ?? "Source").ToUpperInvariant();

        var templateArgs = new TemplateArgs
        {
            Canvas = drawCanvas,
            Article = article,
            Title = displayTitle,
            SourceName = sourceName,
            HeadingFont = headingFont,
            SourceFont = sourceFont,
            BrandFont = brandFont,
            TimestampFont = timestampFont,
            Pad = Config.EXPORT_WIDTH * 0.04f,
            CornerRadius = Config.EXPORT_WIDTH * 0.045f,
            ImageWidth = Config.EXPORT_WIDTH,
            ImageHeight = Config.EXPORT_HEIGHT
        };

        await CreateCustomTemplateAsync(templateArgs);

        // Save
        using var image = SKImage.FromBitmap(canvasBitmap);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        using var stream = File.OpenWrite(outputPath);
        data.SaveTo(stream);

        return outputPath;
    }

    private class TemplateArgs
    {
        public SKCanvas Canvas { get; set; }
        public Article Article { get; set; }
        public string Title { get; set; }
        public string SourceName { get; set; }
        public SKFont HeadingFont { get; set; }
        public SKFont SourceFont { get; set; }
        public SKFont BrandFont { get; set; }
        public SKFont TimestampFont { get; set; }
        public float Pad { get; set; }
        public float CornerRadius { get; set; }
        public int ImageWidth { get; set; }
        public int ImageHeight { get; set; }
    }

    private static SKFont CreateFont(string fontFamily, float size, SKFontStyleWeight weight)
    {
        SKTypeface typeface = null;
#if ANDROID
        try
        {
            var context = Android.App.Application.Context;
            using var stream = context.Assets?.Open(fontFamily);
            if (stream != null)
            {
                using var memoryStream = new System.IO.MemoryStream();
                stream.CopyTo(memoryStream);
                typeface = SKTypeface.FromData(SKData.CreateCopy(memoryStream.ToArray()));
            }
        }
        catch { }
#endif
        if (typeface == null)
        {
            typeface = SKTypeface.FromFamilyName(fontFamily, weight, SKFontStyleWidth.Normal, SKFontStyleSlant.Upright);
        }
        if (typeface == null)
        {
            typeface = SKTypeface.Default;
        }
        return new SKFont(typeface, size);
    }

    // Custom template: Full image cover, bottom 30% semi-transparent overlay, text on top
    private static async Task CreateCustomTemplateAsync(TemplateArgs args)
    {
        var canvas = args.Canvas;
        var W = args.ImageWidth;
        var H = args.ImageHeight;

        // Colors
        var electricRed = new SKColor(0xE5, 0x00, 0x12);  // #E50012 - Orange/Red
        var brightYellow = new SKColor(0xFF, 0xD7, 0x00); // #FFD700 - Yellow
        var pureWhite = SKColors.White;
        var deepBlack = new SKColor(0x00, 0x00, 0x00);

        // Use League Spartan font for all text
        var sourceFont = args.SourceFont;
        var headingFont = args.HeadingFont;
        var brandFont = args.BrandFont;

        // Safe margins
        var margin = W * 0.05f;

        // ========== BRANDING: "360" in YELLOW, "buzz_" in ORANGE (Top-Left) ==========
        var text360 = "360";
        var textBuzz = "buzz_";
        var w360 = brandFont.MeasureText(text360);
        var strokeW = Math.Max(2, W * 0.003f);
        var brandMetrics = brandFont.GetMetrics();

        var brandX = margin;
        var brandY = margin + H * 0.02f;

        // Draw "360" with black stroke then YELLOW fill
        using (var strokePaint = new SKPaint { Color = deepBlack, IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = strokeW })
        using (var fillPaint = new SKPaint { Color = brightYellow, IsAntialias = true })
        {
            canvas.DrawText(text360, brandX, brandY - brandMetrics.Ascent, brandFont, strokePaint);
            canvas.DrawText(text360, brandX, brandY - brandMetrics.Ascent, brandFont, fillPaint);
        }

        // Draw "buzz_" with black stroke then ORANGE fill
        using (var strokePaint = new SKPaint { Color = deepBlack, IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = strokeW })
        using (var fillPaint = new SKPaint { Color = electricRed, IsAntialias = true })
        {
            canvas.DrawText(textBuzz, brandX + w360, brandY - brandMetrics.Ascent, brandFont, strokePaint);
            canvas.DrawText(textBuzz, brandX + w360, brandY - brandMetrics.Ascent, brandFont, fillPaint);
        }

        // ========== BOTTOM 30% SEMI-TRANSPARENT OVERLAY ==========
        var overlayTop = H * 0.70f;  // Start at 70% from top (30% from bottom)
        var overlayHeight = H - overlayTop;
        
        // Semi-transparent dark overlay (dark with 70% opacity)
        using (var overlayPaint = new SKPaint { Color = new SKColor(0, 0, 0, 180), IsAntialias = true })
        {
            canvas.DrawRect(new SKRect(0, overlayTop, W, H), overlayPaint);
        }

        // ========== SOURCE NAME (Top of overlay area, left) ==========
        var sourceMargin = margin;
        var sourceY = overlayTop + margin;

        using (var sourcePaint = new SKPaint { Color = pureWhite, IsAntialias = true })
        {
            canvas.DrawText(args.SourceName, sourceMargin, sourceY - args.SourceFont.GetMetrics().Ascent, args.SourceFont, sourcePaint);
        }

        // ========== TITLE (Below source, large RED text) ==========
        var textLeft = margin;
        var textRight = W - margin;
        var textMaxWidth = textRight - textLeft;

        // Wrap title
        var titleLines = WrapText(canvas, args.Title, headingFont, textMaxWidth);
        if (!titleLines.Any()) titleLines = new List<string> { args.Title };

        // Available space for title
        var titleTop = sourceY + args.SourceFont.GetMetrics().Descent - args.SourceFont.GetMetrics().Ascent + margin;
        var availableHeight = H - margin - titleTop;
        var lineHeight = args.HeadingFont.GetMetrics().Descent - args.HeadingFont.GetMetrics().Ascent + W * 0.01f;

        // Auto-shrink font to fit
        var currentHeadingFont = args.HeadingFont;
        while (titleLines.Count * lineHeight > (H - margin - titleTop) && currentHeadingFont.Size > W * 0.03f)
        {
            currentHeadingFont = CreateFont(FONT_LEAGUE_SPARTAN, currentHeadingFont.Size * 0.9f, SKFontStyleWeight.Bold);
            lineHeight = currentHeadingFont.GetMetrics().Descent - currentHeadingFont.GetMetrics().Ascent + W * 0.01f;
            titleLines = WrapText(canvas, args.Title, currentHeadingFont, W - margin * 2);
        }

        // Draw title lines in RED
        var currentY = titleTop;
        using var titlePaint = new SKPaint { Color = electricRed, IsAntialias = true };
        
        foreach (var line in titleLines)
        {
            if (currentY + currentHeadingFont.GetMetrics().Descent > H - margin)
                break;
            canvas.DrawText(line, margin, currentY - currentHeadingFont.GetMetrics().Ascent, currentHeadingFont, titlePaint);
            currentY += lineHeight;
        }
    }

    private static List<string> WrapText(SKCanvas canvas, string text, SKFont font, float maxWidth)
    {
        var lines = new List<string>();
        foreach (var rawLine in text.Split('\n'))
        {
            var words = rawLine.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            var line = "";
            
            foreach (var word in words)
            {
                var candidate = string.IsNullOrEmpty(line) ? word : line + " " + word;
                var measuredWidth = font.MeasureText(candidate);
                
                if (measuredWidth <= maxWidth || string.IsNullOrEmpty(line))
                {
                    line = candidate;
                }
                else
                {
                    if (!string.IsNullOrEmpty(line))
                        lines.Add(line);
                    line = word;
                }
            }
            if (!string.IsNullOrEmpty(line))
                lines.Add(line);
        }
        return lines;
    }
}