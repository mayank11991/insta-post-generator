using SkiaSharp;
using InstaPostGenerator.Models;
using System.Text.RegularExpressions;
using System.IO;

namespace InstaPostGenerator.Services;

public static class PostGenerator
{
    private static readonly Random _random = new();
    private static readonly Regex DevanagariRegex = new(@"[\u0900-\u097F]");

    // 5 color combos for title text - randomly picked per post
    private static readonly SKColor[][] TitleColorCombos = new[]
    {
        new[] { SKColors.White, new SKColor(0xFF, 0xA5, 0x00), new SKColor(0xFF, 0xFF, 0x00) },         // white + orange + yellow
        new[] { SKColors.White, new SKColor(0x07, 0x77, 0xF6) },                                         // white + #0777F6
        new[] { SKColors.White, new SKColor(0xEA, 0xFF, 0x00) },                                         // white + #EAFF00
        new[] { SKColors.White, new SKColor(0xE5, 0x00, 0x6A) },                                         // white + #E5006A
        new[] { SKColors.White, new SKColor(0xFF, 0xFF, 0x00), new SKColor(0x22, 0x7D, 0xFC) },          // white + yellow + #227DFC
    };

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

        // Dark background first (fills gaps around centered image)
        canvas.Clear(new SKColor(0x10, 0x10, 0x10));

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

            // Draw test image - CONTAIN MODE (center fitted, full quality, entire image visible)
        if (testImage != null)
        {
            var scale = Math.Min((float)Config.EXPORT_WIDTH / testImage.Width, (float)Config.EXPORT_HEIGHT / testImage.Height);
            var newW = Math.Max(1, (int)(testImage.Width * scale));
            var newH = Math.Max(1, (int)(testImage.Height * scale));
            var fitted = testImage.Resize(new SKImageInfo(newW, newH), SKSamplingOptions.Default);
            var offsetX = (Config.EXPORT_WIDTH - newW) / 2;
            var offsetY = (Config.EXPORT_HEIGHT - newH) / 2;
            canvas.DrawBitmap(fitted, offsetX, offsetY);
        }

        var W = Config.EXPORT_WIDTH;
        var H = Config.EXPORT_HEIGHT;

        var headingFont = CreateFont(FONT_LEAGUE_SPARTAN, W * 0.055f, SKFontStyleWeight.Bold);
        var sourceFont = CreateFont(FONT_LEAGUE_SPARTAN, W * 0.025f, SKFontStyleWeight.Bold);
        var timestampFont = CreateFont(FONT_LEAGUE_SPARTAN, W * 0.018f, SKFontStyleWeight.Normal);
        var brandFont = CreateFont(FONT_LEAGUE_SPARTAN, W * 0.042f, SKFontStyleWeight.Bold);

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
        var W = Config.EXPORT_WIDTH;
        var H = Config.EXPORT_HEIGHT;
        using (var canvas = new SKCanvas(canvasBitmap))
        {
            // Dark background first (fills gaps around centered image)
            canvas.Clear(new SKColor(0x10, 0x10, 0x10));
            
            // Draw article image - CONTAIN MODE (center fitted, full quality, entire image visible)
            if (articleBitmap != null)
            {
                var scale = Math.Min((float)Config.EXPORT_WIDTH / articleBitmap.Width, (float)Config.EXPORT_HEIGHT / articleBitmap.Height);
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

    // Custom template: Center-fitted HD image, bottom 1/3 dark fade, red source badge, white headline
    private static async Task CreateCustomTemplateAsync(TemplateArgs args)
    {
        var canvas = args.Canvas;
        var W = args.ImageWidth;
        var H = args.ImageHeight;

        var margin = W * 0.05f;

        // ========== YELLOW BORDER (2px around entire post) ==========
        using (var borderPaint = new SKPaint { Color = new SKColor(0xFF, 0xFF, 0x00), IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 2f })
        {
            canvas.DrawRect(1, 1, W - 2, H - 2, borderPaint);
        }

        // ========== APP NAME: Two vertical lines + "360" orange + "buzz_" yellow ==========
        var appFont = CreateFont(FONT_LEAGUE_SPARTAN, W * 0.032f, SKFontStyleWeight.Normal);
        var appMetrics = appFont.GetMetrics();
        var lineThick = Math.Max(2, W * 0.005f);
        var lineTop = margin;
        var lineBottom = margin + H * 0.035f;

        // Two small vertical lines first (one orange, one yellow)
        using (var linePaint1 = new SKPaint { Color = new SKColor(0xFF, 0xA5, 0x00), IsAntialias = true, StrokeWidth = lineThick, StrokeCap = SKStrokeCap.Round })
        {
            canvas.DrawLine(margin, lineTop, margin, lineBottom, linePaint1);
        }
        var secondLineX = margin + lineThick + W * 0.008f;
        using (var linePaint2 = new SKPaint { Color = new SKColor(0xFF, 0xFF, 0x00), IsAntialias = true, StrokeWidth = lineThick, StrokeCap = SKStrokeCap.Round })
        {
            canvas.DrawLine(secondLineX, lineTop, secondLineX, lineBottom, linePaint2);
        }

        // Vertically center text relative to the lines
        var lineCenterY = (lineTop + lineBottom) / 2f;
        var textBaselineY = lineCenterY - (appMetrics.Ascent + appMetrics.Descent) / 2f;

        // Text starts after the lines
        var textStartX = secondLineX + lineThick + W * 0.012f;
        var text360 = "360";
        var textBuzz = "buzz_";
        var w360 = appFont.MeasureText(text360);

        // "360" in orange
        using (var paint360 = new SKPaint { Color = new SKColor(0xFF, 0xA5, 0x00), IsAntialias = true })
        {
            canvas.DrawText(text360, textStartX, textBaselineY, appFont, paint360);
        }

        // "buzz_" in yellow
        using (var paintBuzz = new SKPaint { Color = new SKColor(0xFF, 0xFF, 0x00), IsAntialias = true })
        {
            canvas.DrawText(textBuzz, textStartX + w360, textBaselineY, appFont, paintBuzz);
        }

        // ========== BOTTOM 1/3: Semi-transparent black blurry fade ==========
        var overlayTop = H * 0.667f;

        // Multi-stop gradient for smoother fade
        using (var overlayPaint = new SKPaint())
        {
            var overlayGradient = SKShader.CreateLinearGradient(
                new SKPoint(0, overlayTop - H * 0.05f),
                new SKPoint(0, H),
                new[] {
                    new SKColor(0, 0, 0, 0),
                    new SKColor(0, 0, 0, 60),
                    new SKColor(0, 0, 0, 160),
                    new SKColor(0, 0, 0, 220)
                },
                new float[] { 0f, 0.2f, 0.5f, 1f },
                SKShaderTileMode.Clamp);
            overlayPaint.Shader = overlayGradient;
            canvas.DrawRect(new SKRect(0, overlayTop - H * 0.05f, W, H), overlayPaint);
        }

        // ========== RED SOURCE BADGE (top of bottom overlay) ==========
        var sourceText = args.SourceName.ToUpperInvariant();
        var sourceBadgeFont = CreateFont(FONT_LEAGUE_SPARTAN, W * 0.028f, SKFontStyleWeight.Bold);
        var sourceBadgeMetrics = sourceBadgeFont.GetMetrics();
        var sourceTextWidth = sourceBadgeFont.MeasureText(sourceText);

        var badgePadX = W * 0.025f;
        var badgePadY = H * 0.008f;
        var badgeW = sourceTextWidth + badgePadX * 2;
        var badgeH = (sourceBadgeMetrics.Descent - sourceBadgeMetrics.Ascent) + badgePadY * 2;
        var badgeX = margin;
        var badgeY = overlayTop + margin * 0.8f;

        // Red rounded rectangle
        using (var badgeBgPaint = new SKPaint { Color = new SKColor(0xFF, 0x00, 0x00), IsAntialias = true })
        {
            var badgeRect = new SKRect(badgeX, badgeY, badgeX + badgeW, badgeY + badgeH);
            canvas.DrawRoundRect(badgeRect, W * 0.008f, W * 0.008f, badgeBgPaint);
        }

        // Source text inside badge
        using (var badgeTextPaint = new SKPaint { Color = SKColors.White, IsAntialias = true })
        {
            canvas.DrawText(sourceText, badgeX + badgePadX, badgeY + badgePadY - sourceBadgeMetrics.Ascent, sourceBadgeFont, badgeTextPaint);
        }

        // ========== HEADLINE (below red badge, center aligned, ~1/4 image height) ==========
        var headlineTop = badgeY + badgeH + margin * 0.6f;
        var headlineAreaBottom = H - margin;
        var headlineAvailableHeight = headlineAreaBottom - headlineTop;

        var textMaxWidth = W - margin * 2;

        // Start with heading font, auto-shrink to fit
        var headlineFont = CreateFont(FONT_LEAGUE_SPARTAN, W * 0.058f, SKFontStyleWeight.Bold);
        var headlineLines = WrapText(canvas, args.Title, headlineFont, textMaxWidth);
        var lineMetrics = headlineFont.GetMetrics();
        var headlineLineHeight = lineMetrics.Descent - lineMetrics.Ascent + W * 0.01f;

        // Shrink if needed
        while (headlineLines.Count * headlineLineHeight > headlineAvailableHeight && headlineFont.Size > W * 0.025f)
        {
            headlineFont = CreateFont(FONT_LEAGUE_SPARTAN, headlineFont.Size * 0.92f, SKFontStyleWeight.Bold);
            lineMetrics = headlineFont.GetMetrics();
            headlineLineHeight = lineMetrics.Descent - lineMetrics.Ascent + W * 0.01f;
            headlineLines = WrapText(canvas, args.Title, headlineFont, textMaxWidth);
        }

        // Max lines that fit
        int maxLines = Math.Max(1, (int)(headlineAvailableHeight / headlineLineHeight));
        var linesToRender = headlineLines.Take(maxLines).ToList();

        // Add ellipsis if truncated
        if (headlineLines.Count > maxLines && linesToRender.Any())
        {
            var lastLine = linesToRender[^1];
            while (headlineFont.MeasureText(lastLine + "...") > textMaxWidth && lastLine.Length > 0)
                lastLine = lastLine[..^1].TrimEnd();
            linesToRender[^1] = lastLine.TrimEnd() + "...";
        }

        // Draw headline center-aligned, each word randomly colored from a picked combo
        var colorCombo = TitleColorCombos[_random.Next(TitleColorCombos.Length)];
        float currentY = headlineTop;
        foreach (var line in linesToRender)
        {
            var words = line.Split(' ');
            // Measure full line width to center the block
            var totalLineWidth = headlineFont.MeasureText(line);
            float cursorX = (W - totalLineWidth) / 2f;

            foreach (var word in words)
            {
                var wordWidth = headlineFont.MeasureText(word);
                var spaceWidth = headlineFont.MeasureText(" ");

                var wordColor = colorCombo[_random.Next(colorCombo.Length)];

                using var wordPaint = new SKPaint { Color = wordColor, IsAntialias = true };
                canvas.DrawText(word, cursorX, currentY - lineMetrics.Ascent, headlineFont, wordPaint);
                cursorX += wordWidth + spaceWidth;
            }

            currentY += headlineLineHeight;
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