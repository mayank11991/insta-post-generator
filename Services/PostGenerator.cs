using SkiaSharp;
using InstaPostGenerator.Models;
using System.Text.RegularExpressions;
using System.IO;

namespace InstaPostGenerator.Services;

public static class PostGenerator
{
    private static readonly Random _random = new();
    private static readonly Regex DevanagariRegex = new(@"[\u0900-\u097F]");

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

        // Dark background
        canvas.Clear(new SKColor(0, 0, 0));

        // Test template
        var headingFont = CreateFont(Config.FONT_ARENA, Config.EXPORT_WIDTH * 0.052f, SKFontStyleWeight.Bold);
        var sourceFont = CreateFont(Config.FONT_ARENA, Config.EXPORT_WIDTH * 0.022f, SKFontStyleWeight.SemiBold);
        var timestampFont = CreateFont(Config.FONT_ARENA, Config.EXPORT_WIDTH * 0.018f, SKFontStyleWeight.Normal);
        var brandFont = CreateFont(Config.FONT_ARENA, Config.EXPORT_WIDTH * 0.042f, SKFontStyleWeight.Bold);

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
            ImageHeight = Config.EXPORT_HEIGHT
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
            
            // Draw article image filling canvas
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

        // Draw template-based layout
        var headingFont = CreateFont(Config.FONT_ARENA, Config.EXPORT_WIDTH * 0.052f, SKFontStyleWeight.Bold);
        var sourceFont = CreateFont(Config.FONT_ARENA, Config.EXPORT_WIDTH * 0.022f, SKFontStyleWeight.SemiBold);
        var timestampFont = CreateFont(Config.FONT_ARENA, Config.EXPORT_WIDTH * 0.018f, SKFontStyleWeight.Normal);
        var brandFont = CreateFont(Config.FONT_ARENA, Config.EXPORT_WIDTH * 0.042f, SKFontStyleWeight.Bold);

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

    // Load template and draw content on it
    private static async Task CreateFromTemplateAsync(TemplateArgs args)
    {
        var canvas = args.Canvas;
        var W = args.ImageWidth;
        var H = args.ImageHeight;

        // Load template image from app package (Resources/Raw/template11.png)
        SKBitmap templateBitmap = null;
        try
        {
            using var stream = await FileSystem.OpenAppPackageFileAsync("template11.png");
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
            // Fallback: draw simple layout
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
        // Template dark color is ~#181616 (24,22,22) - make similar colors transparent
        var pixels = templateBitmap.Pixels;
        for (int i = 0; i < pixels.Length; i++)
        {
            var c = pixels[i];
            var r = c.Red;
            var g = c.Green;
            var b = c.Blue;
            var a = c.Alpha;
            
            // Dark background (~#181616) -> make transparent
            if (r <= 40 && g <= 40 && b <= 40 && a > 0)
            {
                pixels[i] = SKColors.Transparent; // Fully transparent
            }
            // Keep red area (#FF3131) and other colored elements opaque
        }
        templateBitmap.Pixels = pixels;

        // Draw template at native size (0,0 since canvas = template size)
        canvas.DrawBitmap(templateBitmap, 0, 0);

        // Template layout for template11 (1080x1440):
        // Red area starts at ~1140px (y=1140/1440 = 0.79)
        var redAreaTop = H * 0.79f;
        var redAreaBottom = H;
        var redAreaHeight = redAreaBottom - redAreaTop;
        
        var pad = args.Pad;
        var textLeft = pad + W * 0.04f;
        var textRight = W - pad - W * 0.04f;
        var textMaxWidth = textRight - textLeft;

        // 360buzz_ branding at top-left (on top of template)
        var brandX = pad;
        var brandY = pad + W * 0.02f;
        var text360 = "360";
        var textBuzz = "buzz_";
        var w360 = args.BrandFont.MeasureText(text360);
        var strokeW = Math.Max(3, W * 0.004f);
        var brandMetrics = args.BrandFont.GetMetrics();

        // Draw "360" with thick black stroke then YELLOW fill
        using (var strokePaint = new SKPaint { Color = SKColors.Black, IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = strokeW })
        using (var fillPaint = new SKPaint { Color = new SKColor(0xFF, 0xD7, 0x00), IsAntialias = true })
        {
            canvas.DrawText(text360, brandX, brandY - brandMetrics.Ascent, args.BrandFont, strokePaint);
            canvas.DrawText(text360, brandX, brandY - brandMetrics.Ascent, args.BrandFont, fillPaint);
        }

        // Draw "buzz_" with thick black stroke then RED fill
        using (var strokePaint = new SKPaint { Color = SKColors.Black, IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = strokeW })
        using (var fillPaint = new SKPaint { Color = new SKColor(0xE5, 0x00, 0x12), IsAntialias = true })
        {
            canvas.DrawText(textBuzz, brandX + w360, brandY - brandMetrics.Ascent, args.BrandFont, strokePaint);
            canvas.DrawText(textBuzz, brandX + w360, brandY - brandMetrics.Ascent, args.BrandFont, fillPaint);
        }

        // Source name in red area (top of red area)
        var sourceY = redAreaTop + redAreaHeight * 0.15f;
        using (var sourcePaint = new SKPaint { Color = SKColors.White, IsAntialias = true })
        {
            canvas.DrawText(args.SourceName, textLeft, sourceY - args.SourceFont.GetMetrics().Ascent, args.SourceFont, sourcePaint);
        }

        // Title in red area (below source)
        var titleTop = sourceY + args.SourceFont.GetMetrics().Descent - args.SourceFont.GetMetrics().Ascent + W * 0.02f;
        var availableHeight = redAreaBottom - titleTop - W * 0.03f;
        var lineHeight = args.HeadingFont.GetMetrics().Descent - args.HeadingFont.GetMetrics().Ascent + W * 0.008f;

        var titleLines = WrapText(canvas, args.Title, args.HeadingFont, textMaxWidth);
        if (!titleLines.Any()) titleLines = new List<string> { args.Title };

        while (titleLines.Count * lineHeight > availableHeight && args.HeadingFont.Size > W * 0.022f)
        {
            args.HeadingFont = CreateFont(Config.FONT_ARENA, args.HeadingFont.Size * 0.85f, SKFontStyleWeight.Bold);
            lineHeight = args.HeadingFont.GetMetrics().Descent - args.HeadingFont.GetMetrics().Ascent + W * 0.008f;
            titleLines = WrapText(canvas, args.Title, args.HeadingFont, textMaxWidth);
        }

        var currentY = titleTop;
        using var titlePaint = new SKPaint { Color = SKColors.White, IsAntialias = true };
        foreach (var line in titleLines)
        {
            if (currentY + args.HeadingFont.GetMetrics().Descent > redAreaBottom - W * 0.02f)
                break;
            canvas.DrawText(line, textLeft, currentY - args.HeadingFont.GetMetrics().Ascent, args.HeadingFont, titlePaint);
            currentY += lineHeight;
        }

        // Timestamp
        var timestamp = DateTime.Now.ToString("dd MMM yyyy \u2022 HH:mm");
        var tsY = currentY + W * 0.018f;
        if (tsY + args.TimestampFont.GetMetrics().Descent < redAreaBottom - W * 0.015f)
        {
            using var tsPaint = new SKPaint { Color = new SKColor(255, 255, 255, 200), IsAntialias = true };
            canvas.DrawText(timestamp, textLeft, tsY - args.TimestampFont.GetMetrics().Ascent, args.TimestampFont, tsPaint);
        }
    }

    private static void DrawFallbackLayout(TemplateArgs args)
    {
        var canvas = args.Canvas;
        var W = args.ImageWidth;
        var H = args.ImageHeight;
        var pad = args.Pad;

        // Simple fallback: black top, red bottom
        var redTop = H * 0.62f;
        
        using (var blackPaint = new SKPaint { Color = SKColors.Black })
        {
            canvas.DrawRect(new SKRect(0, 0, W, redTop), blackPaint);
        }
        using (var redPaint = new SKPaint { Color = new SKColor(0xE5, 0x00, 0x12) })
        {
            canvas.DrawRect(new SKRect(0, redTop, W, H), redPaint);
        }

        // Source
        var sourceY = redTop + (H - redTop) * 0.15f;
        using (var paint = new SKPaint { Color = SKColors.White, IsAntialias = true })
        {
            canvas.DrawText(args.SourceName, pad, sourceY - args.SourceFont.GetMetrics().Ascent, args.SourceFont, paint);
        }

        // Title
        var titleTop = sourceY + args.SourceFont.GetMetrics().Descent - args.SourceFont.GetMetrics().Ascent + W * 0.02f;
        var textMaxWidth = W - pad * 2;
        var titleLines = WrapText(canvas, args.Title, args.HeadingFont, textMaxWidth);
        if (!titleLines.Any()) titleLines = new List<string> { args.Title };

        var lineHeight = args.HeadingFont.GetMetrics().Descent - args.HeadingFont.GetMetrics().Ascent + W * 0.008f;
        var currentY = titleTop;
        using var titlePaint = new SKPaint { Color = SKColors.White, IsAntialias = true };
        foreach (var line in titleLines)
        {
            if (currentY + args.HeadingFont.GetMetrics().Descent > H - W * 0.02f)
                break;
            canvas.DrawText(line, pad, currentY - args.HeadingFont.GetMetrics().Ascent, args.HeadingFont, titlePaint);
            currentY += lineHeight;
        }
    }

    private static List<string> WrapText(SKCanvas canvas, string text, SKFont font, float maxWidth)
    {
        var lines = new List<string>();
        foreach (var rawLine in text.Split('\n'))
        {
            var words = rawLine.Split(' ');
            var line = "";
            foreach (var word in words)
            {
                var candidate = (line + " " + word).Trim();
                if (font.MeasureText(candidate) <= maxWidth || string.IsNullOrEmpty(line))
                {
                    line = candidate;
                }
                else
                {
                    lines.Add(line);
                    line = word;
                }
            }
            lines.Add(line);
        }
        return lines;
    }
}