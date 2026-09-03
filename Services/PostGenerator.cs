using SkiaSharp;
using InstaPostGenerator.Models;
using System.Text.RegularExpressions;
using System.IO;

namespace InstaPostGenerator.Services;

public static class PostGenerator
{
    private static readonly Random _random = new();
    private static readonly Regex DevanagariRegex = new(@"[\u0900-\u097F]");

    // Template configuration
    private static readonly Dictionary<string, TemplateConfig> _templates = new()
    {
        ["template11"] = new TemplateConfig
        {
            FileName = "template11.png",
            DarkBgThreshold = 40,  // Make dark bg transparent
            RedAreaTopRatio = 0.83f,  // Red starts at 83%
            TextColor = SKColors.White,
            SourceYRatio = 0.12f,
            TitleYRatio = 0.15f,
            SourceFontSizeRatio = 0.12f,
            TitleFontSizeRatio = 0.22f,
            MinTitleFontSizeRatio = 0.08f,
            TextLeftRatio = 0.08f,
            TextRightRatio = 0.08f,
            SourceYOffsetRatio = 0.12f,
            TitleYOffsetRatio = 0.15f,
            AvailableHeightRatio = 0.95f,
            LineHeightRatio = 0.015f,
            MinTitleFontRatio = 0.08f,
            BottomPaddingRatio = 0.03f,
            ApplyBlur = true,
            BlurRadius = 60
        },
        ["template12"] = new TemplateConfig
        {
            FileName = "template12.png",
            DarkBgThreshold = -1,  // No transparency - solid red template
            RedAreaTopRatio = 0.0f,  // Full template
            TextColor = SKColors.White,
            SourceYRatio = 0.15f,
            TitleYRatio = 0.25f,
            SourceFontSizeRatio = 0.10f,
            TitleFontSizeRatio = 0.18f,
            MinTitleFontSizeRatio = 0.07f,
            TextLeftRatio = 0.06f,
            TextRightRatio = 0.06f,
            SourceYOffsetRatio = 0.15f,
            TitleYOffsetRatio = 0.25f,
            AvailableHeightRatio = 0.85f,
            LineHeightRatio = 0.018f,
            MinTitleFontRatio = 0.06f,
            BottomPaddingRatio = 0.05f,
            ApplyBlur = false,
            BlurRadius = 0
        },
        ["template13"] = new TemplateConfig
        {
            FileName = "template13.png",
            DarkBgThreshold = 50,  // Make dark bottom transparent
            RedAreaTopRatio = 0.69f,  // Red accent at 69%
            TextColor = SKColors.White,
            SourceYRatio = 0.10f,
            TitleYRatio = 0.35f,
            SourceFontSizeRatio = 0.10f,
            TitleFontSizeRatio = 0.18f,
            MinTitleFontSizeRatio = 0.07f,
            TextLeftRatio = 0.08f,
            TextRightRatio = 0.08f,
            SourceYOffsetRatio = 0.10f,
            TitleYOffsetRatio = 0.35f,
            AvailableHeightRatio = 0.80f,
            LineHeightRatio = 0.018f,
            MinTitleFontRatio = 0.06f,
            BottomPaddingRatio = 0.05f,
            ApplyBlur = true,
            BlurRadius = 40
        }
    };

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
        var headingFont = CreateFont(Config.FONT_ARENA, Config.EXPORT_WIDTH * 0.052f, SKFontStyleWeight.Bold);
        var sourceFont = CreateFont(Config.FONT_ARENA, Config.EXPORT_WIDTH * 0.022f, SKFontStyleWeight.SemiBold);
        var timestampFont = CreateFont(Config.FONT_ARENA, Config.EXPORT_WIDTH * 0.018f, SKFontStyleWeight.Normal);
        var brandFont = CreateFont(Config.FONT_ARENA, Config.EXPORT_WIDTH * 0.042f, SKFontStyleWeight.Bold);

        var sourceName = (article.Source?.Name ?? "Source").ToUpperInvariant();

        // Randomly select template from available ones
        var availableTemplates = templateIds?.Where(id => id >= 11 && id <= 13).Select(id => $"template{id}").ToArray() 
            ?? new[] { "template11", "template12", "template13" };
        var selectedTemplate = template != 0 ? $"template{template}" : availableTemplates[_random.Next(availableTemplates.Length)];

        var templateArgs = new TemplateArgs
        {
            Canvas = drawCanvas,
            Article = article,
            Title = displayTitle,
            SourceName = sourceName,
            HeadingFont = CreateFont(Config.FONT_ARENA, Config.EXPORT_WIDTH * 0.052f, SKFontStyleWeight.Bold),
            SourceFont = CreateFont(Config.FONT_ARENA, Config.EXPORT_WIDTH * 0.022f, SKFontStyleWeight.SemiBold),
            BrandFont = CreateFont(Config.FONT_ARENA, Config.EXPORT_WIDTH * 0.042f, SKFontStyleWeight.Bold),
            TimestampFont = CreateFont(Config.FONT_ARENA, Config.EXPORT_WIDTH * 0.018f, SKFontStyleWeight.Normal),
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
        public float RedAreaTopRatio { get; set; }
        public SKColor TextColor { get; set; }
        public float SourceYRatio { get; set; }
        public float TitleYRatio { get; set; }
        public float SourceFontSizeRatio { get; set; }
        public float TitleFontSizeRatio { get; set; }
        public float MinTitleFontSizeRatio { get; set; }
        public float TextLeftRatio { get; set; }
        public float TextRightRatio { get; set; }
        public float SourceYOffsetRatio { get; set; }
        public float TitleYOffsetRatio { get; set; }
        public float AvailableHeightRatio { get; set; }
        public float LineHeightRatio { get; set; }
        public float MinTitleFontRatio { get; set; }
        public float BottomPaddingRatio { get; set; }
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

    // Load template and draw content on it
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

        // Draw template at native size
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

        // Calculate layout based on template config
        var redAreaTop = H * config.RedAreaTopRatio;
        var redAreaBottom = H;
        var redAreaHeight = redAreaBottom - redAreaTop;
        
        // Font sizes based on RED AREA height
        var sourceFont = CreateFont(Config.FONT_ARENA, redAreaHeight * config.SourceFontSizeRatio, SKFontStyleWeight.SemiBold);
        var headingFont = CreateFont(Config.FONT_ARENA, redAreaHeight * config.TitleFontSizeRatio, SKFontStyleWeight.Bold);
        
        var textLeft = W * config.TextLeftRatio;
        var textRight = W - W * config.TextRightRatio;
        var textMaxWidth = textRight - textLeft;

        // Source name
        var sourceY = redAreaTop + redAreaHeight * config.SourceYOffsetRatio;
        using (var sourcePaint = new SKPaint { Color = config.TextColor, IsAntialias = true })
        {
            canvas.DrawText(args.SourceName, textLeft, sourceY - sourceFont.GetMetrics().Ascent, sourceFont, sourcePaint);
        }

        // Title in red area
        var titleTop = sourceY + sourceFont.GetMetrics().Descent - sourceFont.GetMetrics().Ascent + redAreaHeight * config.TitleYOffsetRatio;
        var availableHeight = redAreaHeight * config.AvailableHeightRatio;
        var lineHeight = headingFont.GetMetrics().Descent - headingFont.GetMetrics().Ascent + redAreaHeight * config.LineHeightRatio;

        var titleLines = WrapText(canvas, args.Title, headingFont, textMaxWidth);
        if (!titleLines.Any()) titleLines = new List<string> { args.Title };

        // Auto-shrink font to fit area
        while (titleLines.Count * lineHeight > availableHeight && headingFont.Size > redAreaHeight * config.MinTitleFontRatio)
        {
            headingFont = CreateFont(Config.FONT_ARENA, headingFont.Size * 0.9f, SKFontStyleWeight.Bold);
            lineHeight = headingFont.GetMetrics().Descent - headingFont.GetMetrics().Ascent + redAreaHeight * config.LineHeightRatio;
            titleLines = WrapText(canvas, args.Title, headingFont, textMaxWidth);
        }

        var currentY = titleTop;
        using var titlePaint = new SKPaint { Color = config.TextColor, IsAntialias = true };
        foreach (var line in titleLines)
        {
            if (currentY + headingFont.GetMetrics().Descent > H - H * config.BottomPaddingRatio)
                break;
            canvas.DrawText(line, textLeft, currentY - headingFont.GetMetrics().Ascent, headingFont, titlePaint);
            currentY += lineHeight;
        }
    }

    private static void DrawFallbackLayout(TemplateArgs args)
    {
        var canvas = args.Canvas;
        var W = args.ImageWidth;
        var H = args.ImageHeight;
        var pad = args.Pad;

        var redTop = H * 0.62f;
        
        using (var blackPaint = new SKPaint { Color = SKColors.Black })
        {
            canvas.DrawRect(new SKRect(0, 0, W, redTop), blackPaint);
        }
        using (var redPaint = new SKPaint { Color = new SKColor(0xE5, 0x00, 0x12) })
        {
            canvas.DrawRect(new SKRect(0, redTop, W, H), redPaint);
        }

        var sourceY = redTop + (H - redTop) * 0.15f;
        using (var paint = new SKPaint { Color = SKColors.White, IsAntialias = true })
        {
            canvas.DrawText(args.SourceName, pad, sourceY - args.SourceFont.GetMetrics().Ascent, args.SourceFont, paint);
        }

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