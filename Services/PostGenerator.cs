using SkiaSharp;
using InstaPostGenerator.Models;
using System.Text.RegularExpressions;
using System.IO;

namespace InstaPostGenerator.Services;

public static class PostGenerator
{
    private static readonly Random _random = new();
    private static readonly Regex DevanagariRegex = new(@"[\u0900-\u097F]");

    // Template configuration - only template11
    private static readonly Dictionary<string, TemplateConfig> _templates = new()
    {
        ["template11"] = new TemplateConfig
        {
            FileName = "template11.png",
            DarkBgThreshold = 40,  // Make dark bg transparent
            ApplyBlur = true,
            BlurRadius = 60
        }
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

    public static async Task GenerateTestImageAsync(string outputPath, string templateName = "template11")
    {
        var bitmap = new SKBitmap(Config.EXPORT_WIDTH, Config.EXPORT_HEIGHT);
        using var canvas = new SKCanvas(bitmap);

        // Dark background
        canvas.Clear(new SKColor(0, 0, 0));

        // Download a test image for background
        SKBitmap testImage = null;
        try
        {
            using var httpClient = new HttpClient();
            httpClient.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0");
            var imageBytes = await httpClient.GetByteArrayAsync("https://picsum.photos/1080/1440");
            testImage = SKBitmap.Decode(imageBytes);
        }
        catch { }

        // Draw test image - CONTAIN MODE (entire image visible, no crop)
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

        // Apply heavy blur to background for frosted glass effect (if template supports it)
        var config = _templates.GetValueOrDefault(templateName, _templates["template11"]);
        if (config.ApplyBlur)
        {
            using (var blurPaint = new SKPaint { ImageFilter = SKImageFilter.CreateBlur(config.BlurRadius, config.BlurRadius) })
            {
                canvas.SaveLayer(blurPaint);
                canvas.Restore();
            }
        }

        var headingFont = CreateFont(FONT_LEAGUE_SPARTAN, Config.EXPORT_WIDTH * 0.055f, SKFontStyleWeight.Bold);
        var sourceFont = CreateFont(FONT_LEAGUE_SPARTAN, Config.EXPORT_WIDTH * 0.025f, SKFontStyleWeight.Bold);
        var timestampFont = CreateFont(FONT_LEAGUE_SPARTAN, Config.EXPORT_WIDTH * 0.018f, SKFontStyleWeight.Normal);
        var brandFont = CreateFont(FONT_LEAGUE_SPARTAN, Config.EXPORT_WIDTH * 0.042f, SKFontStyleWeight.Bold);

        var templateArgs = new TemplateArgs
        {
            Canvas = canvas,
            Article = null,
            Title = "Breaking News: This is a Test Title for the New Template Layout",
            SourceName = "TEST SOURCE",
            HeadingFont = headingFont,
            SourceFont = sourceFont,
            BrandFont = brandFont,
            TimestampFont = timestampFont,
            Pad = Config.EXPORT_WIDTH * 0.04f,
            CornerRadius = Config.EXPORT_WIDTH * 0.045f,
            ImageWidth = Config.EXPORT_WIDTH,
            ImageHeight = Config.EXPORT_HEIGHT,
            TemplateName = templateName
        };

        await CreateFromTemplateAsync(templateArgs);

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

        // Create canvas with template (EXPORT dimensions = template size)
        var canvasBitmap = new SKBitmap(Config.EXPORT_WIDTH, Config.EXPORT_HEIGHT);
        using (var canvas = new SKCanvas(canvasBitmap))
        {
            canvas.Clear(new SKColor(0, 0, 0));
            
            // Draw article image - CONTAIN MODE (entire image visible, no crop)
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
            
            // Apply heavy blur to background for frosted glass effect
            using (var blurPaint = new SKPaint { ImageFilter = SKImageFilter.CreateBlur(60, 60) })
            {
                canvas.SaveLayer(blurPaint);
                canvas.Restore();
            }
        }

        using var drawCanvas = new SKCanvas(canvasBitmap);

        // Draw template-based layout
        var headingFont = CreateFont(FONT_LEAGUE_SPARTAN, Config.EXPORT_WIDTH * 0.055f, SKFontStyleWeight.Bold);
        var sourceFont = CreateFont(FONT_LEAGUE_SPARTAN, Config.EXPORT_WIDTH * 0.025f, SKFontStyleWeight.Bold);
        var timestampFont = CreateFont(FONT_LEAGUE_SPARTAN, Config.EXPORT_WIDTH * 0.018f, SKFontStyleWeight.Normal);
        var brandFont = CreateFont(FONT_LEAGUE_SPARTAN, Config.EXPORT_WIDTH * 0.042f, SKFontStyleWeight.Bold);

        var sourceName = (article.Source?.Name ?? "Source").ToUpperInvariant();

        // Always use template11
        var selectedTemplate = "template11";

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
            ImageHeight = Config.EXPORT_HEIGHT,
            TemplateName = selectedTemplate
        };

        await CreateFromTemplateAsync(templateArgs);

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
        public string TemplateName { get; set; }
    }

    private class TemplateConfig
    {
        public string FileName { get; set; }
        public int DarkBgThreshold { get; set; }
        public bool ApplyBlur { get; set; }
        public int BlurRadius { get; set; }
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

    // Load template and draw content on it - NEW LAYOUT: No red bar, source with red pill, title in red
    private static async Task CreateFromTemplateAsync(TemplateArgs args)
    {
        var canvas = args.Canvas;
        var W = args.ImageWidth;
        var H = args.ImageHeight;
        var templateName = args.TemplateName ?? "template11";
        var config = _templates.GetValueOrDefault(templateName, _templates["template11"]);

        // Load template image from app package
        SKBitmap templateBitmap = null;
        try
        {
            using var stream = await FileSystem.OpenAppPackageFileAsync(config.FileName);
            if (stream != null)
            {
                using var memoryStream = new MemoryStream();
                await stream.CopyToAsync(memoryStream);
                templateBitmap = SKBitmap.Decode(SKData.CreateCopy(memoryStream.ToArray()));
            }
        }
        catch { }

        if (templateBitmap == null)
        {
            DrawFallbackLayout(args);
            return;
        }

        // Ensure template has alpha (RGBA)
        if (templateBitmap.ColorType != SKColorType.Rgba8888)
        {
            var mutable = templateBitmap.Copy(SKColorType.Rgba8888);
            templateBitmap.Dispose();
            templateBitmap = mutable;
        }

        // Make dark background transparent so article image shows through
        if (config.DarkBgThreshold >= 0)
        {
            var pixels = templateBitmap.Pixels;
            for (int i = 0; i < pixels.Length; i++)
            {
                var c = pixels[i];
                var r = c.Red;
                var g = c.Green;
                var b = c.Blue;
                var a = c.Alpha;
                
                if (r <= config.DarkBgThreshold && g <= config.DarkBgThreshold && b <= config.DarkBgThreshold && a > 0)
                {
                    pixels[i] = SKColors.Transparent;
                }
            }
            templateBitmap.Pixels = pixels;
        }

        // Draw template at native size (template has transparent dark areas, red elements remain)
        canvas.DrawBitmap(templateBitmap, 0, 0);

        // Apply blur if configured
        if (config.ApplyBlur)
        {
            using (var blurPaint = new SKPaint { ImageFilter = SKImageFilter.CreateBlur(config.BlurRadius, config.BlurRadius) })
            {
                canvas.SaveLayer(blurPaint);
                canvas.Restore();
            }
        }

        // ============ NEW LAYOUT ============
        // No red bar at bottom
        // Source: white text with RED pill background (at top-left of safe area)
        // Title: RED text, large, left-aligned, multi-line (bottom area)
        
        // Colors
        var electricRed = new SKColor(0xE5, 0x00, 0x12);  // #E50012
        var pureWhite = SKColors.White;
        var deepBlack = new SKColor(0x00, 0x00, 0x00);

        // Use League Spartan font for all text
        var sourceFont = args.SourceFont;
        var headingFont = args.HeadingFont;

        // Safe margins
        var margin = W * 0.05f;
        var textLeft = margin;
        var textRight = W - margin;
        var textMaxWidth = textRight - textLeft;

        // ========== SOURCE (Top-Left) ==========
        // White text with RED pill background
        var sourceText = args.SourceName;
        var sourcePaddingX = W * 0.025f;
        var sourcePaddingY = W * 0.012f;
        var sourceTextWidth = sourceFont.MeasureText(sourceText);
        var sourcePillWidth = sourceTextWidth + sourcePaddingX * 2;
        var sourcePillHeight = (int)(sourceFont.GetMetrics().Descent - sourceFont.GetMetrics().Ascent + sourcePaddingY * 2);
        var sourcePillRadius = sourcePillHeight / 2f;

        var sourceX = margin;
        var sourceY = margin + H * 0.02f;

        // Draw red pill background
        using (var pillPaint = new SKPaint { Color = new SKColor(0xE5, 0x00, 0x12), IsAntialias = true })
        {
            canvas.DrawRoundRect(
                new SKRect(sourceX, sourceY, sourceX + sourcePillWidth, sourceY + sourcePillHeight),
                sourcePillRadius, sourcePillRadius, pillPaint);
        }

        // Draw white source text on pill
        using (var textPaint = new SKPaint { Color = SKColors.White, IsAntialias = true })
        {
            var textY = sourceY + sourcePaddingY - sourceFont.GetMetrics().Ascent;
            canvas.DrawText(sourceText, sourceX + sourcePaddingX, textY, sourceFont, textPaint);
        }

        // ========== TITLE (Bottom area, left-aligned, RED text) ==========
        // Use headingFont from args (already sized)
        var titleLines = WrapText(canvas, args.Title, headingFont, textMaxWidth);
        if (!titleLines.Any()) titleLines = new List<string> { args.Title };

        // Calculate available space for title (bottom 45% of image)
        var titleAreaTop = H * 0.55f;
        var availableHeight = H - titleAreaTop - margin * 2;
        var lineHeight = headingFont.GetMetrics().Descent - headingFont.GetMetrics().Ascent + W * 0.01f;

        // Auto-shrink font to fit
        while (titleLines.Count * lineHeight > availableHeight && headingFont.Size > W * 0.03f)
        {
            headingFont = CreateFont(FONT_LEAGUE_SPARTAN, headingFont.Size * 0.9f, SKFontStyleWeight.Bold);
            lineHeight = headingFont.GetMetrics().Descent - headingFont.GetMetrics().Ascent + W * 0.01f;
            titleLines = WrapText(canvas, args.Title, headingFont, textMaxWidth);
        }

        // Draw title lines in RED, bottom-aligned area
        var currentY = titleAreaTop;
        using var titlePaint = new SKPaint { Color = electricRed, IsAntialias = true };
        
        foreach (var line in titleLines)
        {
            if (currentY + headingFont.GetMetrics().Descent > H - margin)
                break;
            canvas.DrawText(line, margin, currentY - headingFont.GetMetrics().Ascent, headingFont, titlePaint);
            currentY += lineHeight;
        }
    }

    private static void DrawFallbackLayout(TemplateArgs args)
    {
        var canvas = args.Canvas;
        var fbW = args.ImageWidth;
        var fbH = args.ImageHeight;
        var fbMargin = fbW * 0.05f;

        // Simple fallback: dark top, source pill, red title
        var fbSourceFont = CreateFont(FONT_LEAGUE_SPARTAN, fbW * 0.025f, SKFontStyleWeight.Bold);
        var fbHeadingFont = CreateFont(FONT_LEAGUE_SPARTAN, fbW * 0.055f, SKFontStyleWeight.Bold);

        var fbElectricRed = new SKColor(0xE5, 0x00, 0x12);

        // Source pill
        var fbSourceText = args.SourceName;
        var fbSourcePaddingX = fbW * 0.025f;
        var fbSourcePaddingY = fbW * 0.012f;
        var fbSourceTextWidth = fbSourceFont.MeasureText(args.SourceName);
        var fbPillWidth = fbSourceTextWidth + fbSourcePaddingX * 2;
        var fbPillHeight = (int)(fbSourceFont.GetMetrics().Descent - fbSourceFont.GetMetrics().Ascent + fbSourcePaddingY * 2);
        var fbPillRadius = fbPillHeight / 2f;

        var fbSourceX = fbMargin;
        var fbSourceY = fbMargin;

        using (var pillPaint = new SKPaint { Color = new SKColor(0xE5, 0x00, 0x12), IsAntialias = true })
        {
            canvas.DrawRoundRect(
                new SKRect(fbSourceX, fbSourceY, fbSourceX + fbPillWidth, fbSourceY + fbPillHeight),
                fbPillRadius, fbPillRadius, pillPaint);
        }

        using (var textPaint = new SKPaint { Color = SKColors.White, IsAntialias = true })
        {
            var fbTextY = fbSourceY + fbSourcePaddingY - fbSourceFont.GetMetrics().Ascent;
            canvas.DrawText(args.SourceName, fbSourceX + fbSourcePaddingX, fbTextY, fbSourceFont, textPaint);
        }

        // Title in red
        var fbTitleLines = WrapText(canvas, args.Title, args.HeadingFont, fbW - fbMargin * 2);
        if (!fbTitleLines.Any()) fbTitleLines = new List<string> { args.Title };

        var fbLineHeight = args.HeadingFont.GetMetrics().Descent - args.HeadingFont.GetMetrics().Ascent + fbW * 0.01f;
        var fbCurrentY = fbH * 0.5f;
        using var fbTitlePaint = new SKPaint { Color = fbElectricRed, IsAntialias = true };
        
        foreach (var line in fbTitleLines)
        {
            if (fbCurrentY + args.HeadingFont.GetMetrics().Descent > fbH - fbMargin)
                break;
            canvas.DrawText(line, fbMargin, fbCurrentY - args.HeadingFont.GetMetrics().Ascent, args.HeadingFont, fbTitlePaint);
            fbCurrentY += fbLineHeight;
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