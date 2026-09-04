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

        // Draw test image - CONTAIN MODE (center fitted, entire image visible in HD)
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

        // Apply top and bottom gradient blur effects
        var topBlurHeight = Config.EXPORT_HEIGHT * 0.25f;
        var bottomBlurHeight = Config.EXPORT_HEIGHT * 0.30f;
        var W = Config.EXPORT_WIDTH;
        var H = Config.EXPORT_HEIGHT;
        
        // Top gradient blur (transparent to dark)
        using (var topBlurPaint = new SKPaint())
        {
            var topGradient = SKShader.CreateLinearGradient(
                new SKPoint(0, 0),
                new SKPoint(0, topBlurHeight),
                new[] { new SKColor(0, 0, 0, 160), new SKColor(0, 0, 0, 0) },
                new float[] { 0f, 1f },
                SKShaderTileMode.Clamp);
            topBlurPaint.Shader = topGradient;
            canvas.DrawRect(new SKRect(0, 0, W, topBlurHeight), topBlurPaint);
        }
        
        // Bottom gradient blur (dark to transparent)
        using (var bottomBlurPaint = new SKPaint())
        {
            var bottomGradient = SKShader.CreateLinearGradient(
                new SKPoint(0, H - bottomBlurHeight),
                new SKPoint(0, H),
                new[] { new SKColor(0, 0, 0, 0), new SKColor(0, 0, 0, 200) },
                new float[] { 0f, 1f },
                SKShaderTileMode.Clamp);
            bottomBlurPaint.Shader = bottomGradient;
            canvas.DrawRect(new SKRect(0, H - bottomBlurHeight, W, H), bottomBlurPaint);
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
        var W = Config.EXPORT_WIDTH;
        var H = Config.EXPORT_HEIGHT;
        using (var canvas = new SKCanvas(canvasBitmap))
        {
            // White background fallback
            canvas.Clear(new SKColor(255, 255, 255));
            
            // Draw article image - CONTAIN MODE (center fitted, entire image visible in HD)
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
            
            // Apply top and bottom gradient blur effects
            var topBlurHeight = H * 0.25f;
            var bottomBlurHeight = H * 0.30f;
            
            // Top gradient blur (transparent to dark)
            using (var topBlurPaint = new SKPaint())
            {
                var topGradient = SKShader.CreateLinearGradient(
                    new SKPoint(0, 0),
                    new SKPoint(0, topBlurHeight),
                    new[] { new SKColor(0, 0, 0, 160), new SKColor(0, 0, 0, 0) },
                    new float[] { 0f, 1f },
                    SKShaderTileMode.Clamp);
                topBlurPaint.Shader = topGradient;
                canvas.DrawRect(new SKRect(0, 0, W, topBlurHeight), topBlurPaint);
            }
            
            // Bottom gradient blur (dark to transparent)
            using (var bottomBlurPaint = new SKPaint())
            {
                var bottomGradient = SKShader.CreateLinearGradient(
                    new SKPoint(0, H - bottomBlurHeight),
                    new SKPoint(0, H),
                    new[] { new SKColor(0, 0, 0, 0), new SKColor(0, 0, 0, 200) },
                    new float[] { 0f, 1f },
                    SKShaderTileMode.Clamp);
                bottomBlurPaint.Shader = bottomGradient;
                canvas.DrawRect(new SKRect(0, H - bottomBlurHeight, W, H), bottomBlurPaint);
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

    // Custom template: Center-fitted HD image, top/bottom gradient blur, text at bottom 30%
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
        using (var fillPaint = new SKPaint { Color = new SKColor(0xFF, 0xD7, 0x00), IsAntialias = true })
        {
            canvas.DrawText(text360, brandX, brandY - brandMetrics.Ascent, brandFont, strokePaint);
            canvas.DrawText(text360, brandX, brandY - brandMetrics.Ascent, brandFont, fillPaint);
        }

        // Draw "buzz_" with black stroke then ORANGE fill
        using (var strokePaint = new SKPaint { Color = deepBlack, IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = strokeW })
        using (var fillPaint = new SKPaint { Color = new SKColor(0xE5, 0x00, 0x12), IsAntialias = true })
        {
            canvas.DrawText(textBuzz, brandX + w360, brandY - brandMetrics.Ascent, brandFont, strokePaint);
            canvas.DrawText(textBuzz, brandX + w360, brandY - brandMetrics.Ascent, brandFont, fillPaint);
        }

        // ========== TEXT AT BOTTOM 30% AREA ==========
        var overlayTop = H * 0.70f;  // Bottom 30% starts here
        var overlayHeight = H - overlayTop;

        // Semi-transparent dark overlay at bottom 30% (gradient)
        using (var overlayPaint = new SKPaint())
        {
            var overlayGradient = SKShader.CreateLinearGradient(
                new SKPoint(0, overlayTop),
                new SKPoint(0, H),
                new[] { new SKColor(0, 0, 0, 0), new SKColor(0, 0, 0, 200) },
                new float[] { 0f, 1f },
                SKShaderTileMode.Clamp);
            overlayPaint.Shader = overlayGradient;
            canvas.DrawRect(new SKRect(0, overlayTop, W, H), overlayPaint);
        }

        // ========== SOURCE NAME (Left, at BOTTOM of overlay) ==========
        var sourceMargin = margin;
        var sourceY = H - margin;  // Bottom of screen

        using (var sourcePaint = new SKPaint { Color = SKColors.White, IsAntialias = true })
        {
            canvas.DrawText(args.SourceName, margin, sourceY - args.SourceFont.GetMetrics().Ascent, args.SourceFont, sourcePaint);
        }

        // ========== TITLE (Above source, mixed white/yellow/orange) ==========
        var textLeft = margin;
        var textRight = W - margin;
        var textMaxWidth = textRight - textLeft;

        // Wrap title
        var titleLines = WrapText(canvas, args.Title, args.HeadingFont, textMaxWidth);
        if (!titleLines.Any()) titleLines = new List<string> { args.Title };

        // Available space for title (between source and top of overlay)
        var titleBottom = sourceY - margin;
        var availableHeight = titleBottom - overlayTop;

        // Create local font that can be shrunk
        var localHeadingFont = args.HeadingFont;
        var lineHeight = localHeadingFont.GetMetrics().Descent - localHeadingFont.GetMetrics().Ascent + W * 0.008f;

        // Auto-shrink font to fit within overlay area
        while (titleLines.Count * lineHeight > availableHeight && localHeadingFont.Size > W * 0.03f)
        {
            localHeadingFont = CreateFont(FONT_LEAGUE_SPARTAN, localHeadingFont.Size * 0.9f, SKFontStyleWeight.Bold);
            lineHeight = localHeadingFont.GetMetrics().Descent - localHeadingFont.GetMetrics().Ascent + W * 0.008f;
            titleLines = WrapText(canvas, args.Title, localHeadingFont, W - margin * 2);
        }

        // Draw title from bottom up (bottom-aligned, going upward)
        var titlePaintWhite = new SKPaint { Color = SKColors.White, IsAntialias = true };
        var titlePaintYellow = new SKPaint { Color = new SKColor(0xFF, 0xD7, 0x00), IsAntialias = true };
        var titlePaintOrange = new SKPaint { Color = new SKColor(0xFF, 0xA5, 0x00), IsAntialias = true };
        
        // Start from bottom of title area (just above source)
        var currentY = titleBottom;
        
        foreach (var line in titleLines)
        {
            if (currentY - lineHeight < overlayTop + margin)
                break;

            currentY -= lineHeight;

            // Draw each word with random color (white/yellow/orange)
            var words = line.Split(' ');
            float currentX = margin;
            foreach (var word in words)
            {
                var wordWidth = localHeadingFont.MeasureText(word);
                var spaceWidth = localHeadingFont.MeasureText(" ");
                
                // Random color: 40% white, 30% yellow, 30% orange
                var rand = _random.NextDouble();
                SKPaint wordPaint;
                if (rand < 0.4)
                    wordPaint = new SKPaint { Color = SKColors.White, IsAntialias = true };
                else if (rand < 0.7)
                    wordPaint = new SKPaint { Color = new SKColor(0xFF, 0xD7, 0x00), IsAntialias = true };
                else
                    wordPaint = new SKPaint { Color = new SKColor(0xFF, 0xA5, 0x00), IsAntialias = true };
                
                canvas.DrawText(word, currentX, currentY - localHeadingFont.GetMetrics().Ascent, localHeadingFont, wordPaint);
                currentX += wordWidth + spaceWidth;
            }
            
            currentY -= lineHeight;
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