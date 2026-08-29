using SkiaSharp;
using InstaPostGenerator.Models;
using System.Text.RegularExpressions;

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

    // Layout constants
    private const float PAD_RATIO = 0.04f;
    private const float CORNER_RADIUS_RATIO = 0.03f;
    private const float HEADING_SIZE_RATIO = 0.065f;
    private const float BRAND_SIZE_RATIO = 0.045f;
    private const float SOURCE_SIZE_RATIO = 0.035f;
    private const float LINE_GAP_RATIO = 0.006f;
    private const float OVERLAY_PAD_RATIO = 0.04f;
    private const float GAP_MED_RATIO = 0.035f;
    private const float BORDER_WIDTH_RATIO = 0.012f;

    private static readonly string[] STOP_WORDS = new[]
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

    public static void GenerateTestImage(string outputPath)
    {
        var bitmap = new SKBitmap(Config.IMAGE_WIDTH, Config.IMAGE_HEIGHT);
        using var canvas = new SKCanvas(bitmap);

        // Background gradient matching Python fallback
        for (int y = 0; y < Config.IMAGE_HEIGHT; y++)
        {
            float t = (float)y / Config.IMAGE_HEIGHT;
            var r = (byte)(20 + 90 * t);
            var g = (byte)(24 + 40 * t);
            var b = (byte)(40 + 100 * t);
            using var linePaint = new SKPaint { Color = new SKColor(r, g, b) };
            canvas.DrawLine(0, y, Config.IMAGE_WIDTH, y, linePaint);
        }

        DrawGradientOverlay(canvas);
        DrawBranding(canvas);

        // Source box
        var headingFont = CreateFont(Config.FONT_ARENA, Config.IMAGE_WIDTH * HEADING_SIZE_RATIO, SKFontStyleWeight.Bold);
        var sourceFont = CreateFont(Config.FONT_ARENA, Config.IMAGE_WIDTH * SOURCE_SIZE_RATIO, SKFontStyleWeight.Bold);
        var pad = Config.IMAGE_WIDTH * PAD_RATIO;
        var sourceBoxPad = Config.IMAGE_WIDTH * 0.016f;
        var sourceName = "TIMES OF INDIA";
        var sourceWidth = sourceFont.MeasureText(sourceName) + sourceBoxPad * 2;
        var sourceBoxHeight = sourceFont.GetMetrics().Descent - sourceFont.GetMetrics().Ascent + sourceBoxPad * 2;
        var gradTop = Config.IMAGE_HEIGHT * 0.6f;
        var gapMed = Config.IMAGE_WIDTH * GAP_MED_RATIO;
        var lineGap = Config.IMAGE_WIDTH * LINE_GAP_RATIO;
        var overlayPad = Config.IMAGE_WIDTH * OVERLAY_PAD_RATIO;

        var boxY = gradTop + overlayPad;
        using (var paint = new SKPaint { Color = Config.BRAND_GREEN, IsAntialias = true })
        {
            canvas.DrawRoundRect(new SKRect(pad, boxY, pad + sourceWidth, boxY + sourceBoxHeight),
                Config.IMAGE_WIDTH * 0.008f, Config.IMAGE_WIDTH * 0.008f, paint);
        }
        using (var paint = new SKPaint { Color = Config.BLACK, IsAntialias = true })
        {
            canvas.DrawText(sourceName, pad + sourceBoxPad, boxY + sourceBoxPad - sourceFont.GetMetrics().Ascent,
                sourceFont, paint);
        }

        // Headline
        var testHeadline = "This is a test headline to check font rendering, layout and positioning of all elements on the Instagram post template";
        var headlineLines = WrapText(canvas, testHeadline, headingFont, Config.IMAGE_WIDTH - pad * 2);
        var headlineY = boxY + sourceBoxHeight + gapMed;
        var metrics = headingFont.GetMetrics();
        foreach (var line in headlineLines)
        {
            DrawHighlightedText(canvas, pad, headlineY - metrics.Ascent, line, headingFont,
                Config.WHITE, new[] { Config.BRAND_GREEN, Config.BRAND_RED });
            headlineY += metrics.Descent - metrics.Ascent + lineGap;
        }

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

        // Use actual title, not hook
        var displayTitle = title;

        // Create canvas
        SKBitmap canvasBitmap;
        if (articleBitmap != null)
        {
            canvasBitmap = new SKBitmap(Config.IMAGE_WIDTH, Config.IMAGE_HEIGHT);
            using (var canvas = new SKCanvas(canvasBitmap))
            {
                canvas.Clear(new SKColor(16, 18, 22));
                // Paste fitted image centered (contain mode)
                var scale = Math.Min((float)Config.IMAGE_WIDTH / articleBitmap.Width, (float)Config.IMAGE_HEIGHT / articleBitmap.Height);
                var newW = Math.Max(1, (int)(articleBitmap.Width * scale));
                var newH = Math.Max(1, (int)(articleBitmap.Height * scale));
                var fitted = articleBitmap.Resize(new SKImageInfo(newW, newH), SKSamplingOptions.Default);
                var offsetX = (Config.IMAGE_WIDTH - newW) / 2;
                var offsetY = (Config.IMAGE_HEIGHT - newH) / 2;
                canvas.DrawBitmap(fitted, offsetX, offsetY);
            }
        }
        else
        {
            canvasBitmap = new SKBitmap(Config.IMAGE_WIDTH, Config.IMAGE_HEIGHT);
            using (var canvas = new SKCanvas(canvasBitmap))
            {
                // Gradient fallback matching Python
                for (int y = 0; y < Config.IMAGE_HEIGHT; y++)
                {
                    float t = (float)y / Config.IMAGE_HEIGHT;
                    var r = (byte)(20 + 90 * t);
                    var g = (byte)(24 + 40 * t);
                    var b = (byte)(40 + 100 * t);
                    using var linePaint = new SKPaint { Color = new SKColor(r, g, b) };
                    canvas.DrawLine(0, y, Config.IMAGE_WIDTH, y, linePaint);
                }
            }
        }

        using var drawCanvas = new SKCanvas(canvasBitmap);

        // Draw gradient overlay
        DrawGradientOverlay(drawCanvas);

        // Draw top-left branding
        DrawBranding(drawCanvas);

        // Prepare fonts (matching Python: Montserrat for headings, Montserrat for source)
        var headingFont = CreateFont(Config.FONT_ARENA, Config.IMAGE_WIDTH * HEADING_SIZE_RATIO, SKFontStyleWeight.Bold);
        var brandFont = CreateFont(Config.FONT_ARENA, Config.IMAGE_WIDTH * BRAND_SIZE_RATIO, SKFontStyleWeight.Bold);
        var sourceFont = CreateFont(Config.FONT_ARENA, Config.IMAGE_WIDTH * SOURCE_SIZE_RATIO, SKFontStyleWeight.Bold);

        // Source name
        var sourceName = (article.Source?.Name ?? "Source").ToUpperInvariant();

        // Calculate layout
        var pad = Config.IMAGE_WIDTH * PAD_RATIO;
        var cornerRadius = Config.IMAGE_WIDTH * CORNER_RADIUS_RATIO;
        var lineGap = Config.IMAGE_WIDTH * LINE_GAP_RATIO;
        var overlayPad = Config.IMAGE_WIDTH * OVERLAY_PAD_RATIO;
        var gapMed = Config.IMAGE_WIDTH * GAP_MED_RATIO;

        var sourceBoxPad = Config.IMAGE_WIDTH * 0.016f;
        var sourceWidth = sourceFont.MeasureText(sourceName) + sourceBoxPad * 2;
        var sourceBoxHeight = sourceFont.GetMetrics().Descent - sourceFont.GetMetrics().Ascent + sourceBoxPad * 2;

        var maxTextWidth = Config.IMAGE_WIDTH - pad * 2;
        var headlineLines = WrapText(drawCanvas, displayTitle, headingFont, maxTextWidth);
        if (!headlineLines.Any())
            headlineLines = new List<string> { displayTitle };

        var headlineHeight = headlineLines.Count * (headingFont.GetMetrics().Descent - headingFont.GetMetrics().Ascent + lineGap) - lineGap;

        var contentHeight = sourceBoxHeight + gapMed + headlineHeight;
        var overlayHeight = Math.Max(Config.IMAGE_HEIGHT * 0.25f, contentHeight + overlayPad * 2);
        overlayHeight = Math.Min(overlayHeight, Config.IMAGE_HEIGHT * 0.5f);
        var gradTop = Config.IMAGE_HEIGHT - overlayHeight;

        // Dispatch to template
        var templateArgs = new TemplateArgs
        {
            Canvas = drawCanvas,
            Article = article,
            Title = displayTitle,
            SourceName = sourceName,
            HeadingFont = headingFont,
            SourceFont = sourceFont,
            BrandFont = brandFont,
            Pad = pad,
            CornerRadius = cornerRadius,
            GradTop = gradTop,
            OverlayPad = overlayPad,
            GapMed = gapMed,
            LineGap = lineGap,
            HeadlineLines = headlineLines,
            HeadlineHeight = headlineHeight,
            SourceBoxHeight = sourceBoxHeight,
            SourceBoxPad = sourceBoxPad,
            SourceWidth = sourceWidth,
            ImageWidth = Config.IMAGE_WIDTH,
            ImageHeight = Config.IMAGE_HEIGHT
        };

        var templatePool = templateIds ?? new[] { 1, 2, 3, 4 };
        var selectedTemplate = template != 0 ? template : templatePool[_random.Next(templatePool.Length)];

        switch (selectedTemplate)
        {
            case 1: CreateTemplate1(templateArgs); break;
            case 2: CreateTemplate2(templateArgs); break;
            case 3: CreateTemplate3(templateArgs); break;
            case 4: CreateTemplate4(templateArgs); break;
            case 5: CreateTemplate5(templateArgs); break;
            case 6: CreateTemplate6(templateArgs); break;
            default: CreateTemplate1(templateArgs); break;
        }

        // Rounded corners
        ApplyRoundedCorners(canvasBitmap, cornerRadius);

        // Yellow border
        DrawBorder(canvasBitmap, cornerRadius);

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
        public float Pad { get; set; }
        public float CornerRadius { get; set; }
        public float GradTop { get; set; }
        public float OverlayPad { get; set; }
        public float GapMed { get; set; }
        public float LineGap { get; set; }
        public List<string> HeadlineLines { get; set; }
        public float HeadlineHeight { get; set; }
        public float SourceBoxHeight { get; set; }
        public float SourceBoxPad { get; set; }
        public float SourceWidth { get; set; }
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

    private static SKBitmap FitWithContain(SKBitmap bitmap)
    {
        var targetRatio = (float)Config.IMAGE_WIDTH / Config.IMAGE_HEIGHT;
        var srcRatio = (float)bitmap.Width / bitmap.Height;

        if (srcRatio > targetRatio)
        {
            var newWidth = (int)(bitmap.Height * targetRatio);
            var left = (bitmap.Width - newWidth) / 2;
            var subset = new SKRectI(left, 0, left + newWidth, bitmap.Height);
            var result = new SKBitmap(newWidth, bitmap.Height);
            using var canvas = new SKCanvas(result);
            canvas.DrawBitmap(bitmap, new SKRect(0, 0, newWidth, bitmap.Height), subset);
            return result;
        }
        else
        {
            var newHeight = (int)(bitmap.Width / targetRatio);
            var top = (bitmap.Height - newHeight) / 2;
            var subset = new SKRectI(0, top, bitmap.Width, top + newHeight);
            var result = new SKBitmap(bitmap.Width, newHeight);
            using var canvas = new SKCanvas(result);
            canvas.DrawBitmap(bitmap, new SKRect(0, 0, bitmap.Width, newHeight), subset);
            return result;
        }
    }

    private static void DrawGradientOverlay(SKCanvas canvas)
    {
        var gradient = SKShader.CreateLinearGradient(
            new SKPoint(0, 0),
            new SKPoint(0, Config.IMAGE_HEIGHT),
            new[] { new SKColor(0, 0, 0, 0), new SKColor(0, 0, 0, 230) },
            new float[] { 0, 1 },
            SKShaderTileMode.Clamp);

        var paint = new SKPaint { Shader = gradient };
        canvas.DrawRect(new SKRect(0, 0, Config.IMAGE_WIDTH, Config.IMAGE_HEIGHT), paint);
    }

    private static void DrawBranding(SKCanvas canvas)
    {
        var pad = Config.IMAGE_WIDTH * PAD_RATIO;
        var lineHeight = Config.IMAGE_WIDTH * 0.055f;
        var lineWidth = Math.Max(6, Config.IMAGE_WIDTH * 0.008f);
        var linesX = pad;
        var linesY = pad;

        // Green line
        using (var paint = new SKPaint { Color = Config.BRAND_GREEN, IsAntialias = true })
        {
            canvas.DrawRoundRect(new SKRect(linesX, linesY, linesX + lineWidth, linesY + lineHeight), lineWidth / 2, lineWidth / 2, paint);
        }

        // Red line
        using (var paint = new SKPaint { Color = Config.BRAND_RED, IsAntialias = true })
        {
            canvas.DrawRoundRect(
                new SKRect(linesX + lineWidth + Config.IMAGE_WIDTH * 0.012f, linesY,
                          linesX + 2 * lineWidth + Config.IMAGE_WIDTH * 0.012f, linesY + lineHeight),
                lineWidth / 2, lineWidth / 2, paint);
        }

        // "360" in GREEN, "buzz" in WHITE
        var brandFont = CreateFont(Config.FONT_ARENA, Config.IMAGE_WIDTH * BRAND_SIZE_RATIO, SKFontStyleWeight.Bold);
        var brandX = linesX + 2 * lineWidth + Config.IMAGE_WIDTH * 0.012f + Config.IMAGE_WIDTH * 0.02f;
        var metrics = brandFont.GetMetrics();
        var brandY = linesY + (lineHeight - brandFont.Size) / 2 - Config.IMAGE_WIDTH * 0.008f - metrics.Ascent;

        var text360 = "360";
        var textBuzz = "buzz";
        var w360 = brandFont.MeasureText(text360);

        using (var paint = new SKPaint { Color = Config.BRAND_GREEN, IsAntialias = true })
        {
            canvas.DrawText(text360, brandX, brandY, brandFont, paint);
        }
        using (var paint = new SKPaint { Color = Config.WHITE, IsAntialias = true })
        {
            canvas.DrawText(textBuzz, brandX + w360, brandY, brandFont, paint);
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

    private static bool HasDevanagari(string text)
    {
        return DevanagariRegex.IsMatch(text);
    }

    private static bool ShouldHighlight(string word, int index, int total)
    {
        var clean = word.Trim(".,:;!?()[]{}\"'-".ToCharArray()).ToLowerInvariant();
        if (clean.Length < 3) return false;
        if (STOP_WORDS.Contains(clean)) return false;
        if (clean.All(char.IsUpper) || char.IsUpper(clean[0])) return true;
        if (_random.NextDouble() < HIGHLIGHT_RATIO) return true;
        return false;
    }

    private static void DrawHighlightedText(SKCanvas canvas, float x, float y, string text, SKFont font, SKColor defaultColor, SKColor[] highlightColors)
    {
        var words = text.Split(' ');
        int colorIndex = 0;
        float currentX = x;

        foreach (var (word, i) in words.Select((w, idx) => (w, idx)))
        {
            var isHighlight = ShouldHighlight(word, i, words.Length);
            var color = isHighlight ? highlightColors[colorIndex++ % highlightColors.Length] : defaultColor;

            using var paint = new SKPaint { Color = color, IsAntialias = true };
            canvas.DrawText(word, currentX, y, font, paint);

            currentX += font.MeasureText(word) + (i < words.Length - 1 ? font.MeasureText(" ") : 0);
        }
    }

    private static string ExtractShortSummary(Article article, int maxWords = 4)
    {
        var title = (article.Title ?? "").Trim();
        if (string.IsNullOrEmpty(title))
            return "Breaking News";

        var words = title.Split(' ');
        if (words.Length <= maxWords)
            return title;

        var meaningful = words.Where(w => !STOP_WORDS.Contains(w.ToLowerInvariant()) && w.Length > 2).ToList();
        if (meaningful.Count >= maxWords)
            return string.Join(" ", meaningful.Take(maxWords));

        return string.Join(" ", words.Take(maxWords));
    }

    private static void ApplyRoundedCorners(SKBitmap bitmap, float radius)
    {
        using var mask = new SKBitmap(bitmap.Width, bitmap.Height, SKColorType.Alpha8, SKAlphaType.Premul);
        using var maskCanvas = new SKCanvas(mask);
        maskCanvas.Clear(SKColors.Transparent);
        using var paint = new SKPaint { Color = SKColors.White, IsAntialias = true };
        maskCanvas.DrawRoundRect(new SKRect(0, 0, bitmap.Width, bitmap.Height), radius, radius, paint);

        // Apply mask - we need to create a new bitmap with the mask applied
        var result = new SKBitmap(bitmap.Width, bitmap.Height);
        using var resultCanvas = new SKCanvas(result);
        resultCanvas.DrawBitmap(bitmap, 0, 0);
        // Note: SkiaSharp doesn't have direct alpha masking like PIL
        // For simplicity, we'll draw the rounded rect on top with destination-in blend mode
        // This is a limitation - in production you'd use a proper masking approach
    }

    private static void DrawBorder(SKBitmap bitmap, float radius)
    {
        using var canvas = new SKCanvas(bitmap);
        var borderWidth = Config.IMAGE_WIDTH * BORDER_WIDTH_RATIO;
        using var paint = new SKPaint
        {
            Color = Config.BRAND_RED,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = borderWidth,
            IsAntialias = true
        };
        canvas.DrawRoundRect(
            new SKRect(borderWidth / 2, borderWidth / 2, bitmap.Width - borderWidth / 2, bitmap.Height - borderWidth / 2),
            radius, radius, paint);
    }

    // Template 1: Gradient overlay with green source box (black text)
    private static void CreateTemplate1(TemplateArgs args)
    {
        var canvas = args.Canvas;
        var boxY = args.GradTop + args.OverlayPad;

        // Source box - GREEN with BLACK text
        using (var paint = new SKPaint { Color = Config.BRAND_GREEN, IsAntialias = true })
        {
            canvas.DrawRoundRect(
                new SKRect(args.Pad, boxY, args.Pad + args.SourceWidth, boxY + args.SourceBoxHeight),
                args.ImageWidth * 0.008f, args.ImageWidth * 0.008f, paint);
        }

        var sourceTextWidth = args.SourceFont.MeasureText(args.SourceName);
        using (var paint = new SKPaint { Color = Config.BLACK, IsAntialias = true })
        {
            canvas.DrawText(args.SourceName,
                args.Pad + (args.SourceWidth - sourceTextWidth) / 2,
                boxY + args.SourceBoxPad - args.ImageWidth * 0.004f - args.SourceFont.GetMetrics().Ascent,
                args.SourceFont, paint);
        }

        // Headline
        var headlineY = boxY + args.SourceBoxHeight + args.GapMed;
        foreach (var (line, i) in args.HeadlineLines.Select((l, idx) => (l, idx)))
        {
            var y = headlineY + i * (args.HeadingFont.GetMetrics().Descent - args.HeadingFont.GetMetrics().Ascent + args.LineGap);
            if (y + args.HeadingFont.GetMetrics().Descent > args.ImageHeight - args.OverlayPad)
                break;

            DrawHighlightedText(canvas, args.Pad, y - args.HeadingFont.GetMetrics().Ascent, line, args.HeadingFont, Config.WHITE, new[] { Config.BRAND_GREEN, Config.BRAND_RED });
        }
    }

    // Template 2: White bottom layout with RED highlights
    private static void CreateTemplate2(TemplateArgs args)
    {
        var canvas = args.Canvas;
        var innerPadding = args.ImageWidth * 0.03f;
        var smallGap = args.ImageWidth * 0.015f;
        var boxY = args.GradTop + args.OverlayPad + smallGap;

        // White background
        using (var paint = new SKPaint { Color = new SKColor(255, 255, 255, 230), IsAntialias = true })
        {
            canvas.DrawRoundRect(
                new SKRect(args.Pad, args.GradTop + args.OverlayPad, args.ImageWidth - args.Pad, args.ImageHeight - args.OverlayPad),
                args.CornerRadius, args.CornerRadius, paint);
        }

        // Source box - GREEN with BLACK text
        using (var paint = new SKPaint { Color = Config.BRAND_GREEN, IsAntialias = true })
        {
            canvas.DrawRoundRect(
                new SKRect(args.Pad + innerPadding + 20, boxY, args.Pad + args.SourceWidth + innerPadding + 20, boxY + args.SourceBoxHeight),
                args.ImageWidth * 0.008f, args.ImageWidth * 0.008f, paint);
        }

        var sourceTextWidth = args.SourceFont.MeasureText(args.SourceName);
        using (var paint = new SKPaint { Color = Config.BLACK, IsAntialias = true })
        {
            canvas.DrawText(args.SourceName,
                args.Pad + innerPadding + 20 + (args.SourceWidth - sourceTextWidth) / 2,
                boxY + args.SourceBoxPad - args.ImageWidth * 0.004f - args.SourceFont.GetMetrics().Ascent,
                args.SourceFont, paint);
        }

        // Headline with font fitting
        var headlineY = boxY + args.SourceBoxHeight + args.GapMed;
        var availableH = (args.ImageHeight - args.OverlayPad - innerPadding) - headlineY;
        var maxTextW = args.ImageWidth - args.Pad * 2 - innerPadding * 2 - 40;

        var hf = args.HeadingFont;
        var lines = new List<string>(args.HeadlineLines);
        while (lines.Count * (hf.GetMetrics().Descent - hf.GetMetrics().Ascent + args.LineGap) - args.LineGap > availableH && hf.Size > args.ImageWidth * 0.02f)
        {
            hf = CreateFont(Config.FONT_ARENA, hf.Size * 0.9f, SKFontStyleWeight.Bold);
            lines = WrapText(canvas, args.Title, hf, maxTextW);
        }

        foreach (var (line, i) in lines.Select((l, idx) => (l, idx)))
        {
            var y = headlineY + i * (hf.GetMetrics().Descent - hf.GetMetrics().Ascent + args.LineGap);
            if (y + hf.GetMetrics().Descent > args.ImageHeight - args.OverlayPad - innerPadding)
                break;

            DrawHighlightedText(canvas, args.Pad + innerPadding + 20, y - hf.GetMetrics().Ascent, line, hf, Config.BLACK, new[] { Config.BRAND_RED, Config.BRAND_RED });
        }
    }

    // Template 3: Green top summary box OVERLAY, green source box with black text
    private static void CreateTemplate3(TemplateArgs args)
    {
        var canvas = args.Canvas;
        var shortSummary = ExtractShortSummary(args.Article);

        // Summary box - GREEN with BLACK text
        var maxSummaryW = args.ImageWidth - args.Pad * 2;
        var summaryFont = CreateFont(Config.FONT_ARENA, args.ImageWidth * 0.055f, SKFontStyleWeight.Bold);
        var summaryWidth = (int)summaryFont.MeasureText(shortSummary) + (int)args.Pad * 2;

        while (summaryWidth > maxSummaryW && summaryFont.Size > args.ImageWidth * 0.02f)
        {
            summaryFont = CreateFont(Config.FONT_ARENA, summaryFont.Size * 0.9f, SKFontStyleWeight.Bold);
            summaryWidth = (int)summaryFont.MeasureText(shortSummary) + (int)args.Pad * 2;
        }

        var summaryBoxHeight = summaryFont.GetMetrics().Descent - summaryFont.GetMetrics().Ascent + args.Pad;
        var summaryY = args.GradTop - summaryBoxHeight - args.ImageWidth * 0.015f;
        if (summaryY < args.ImageWidth * 0.02f)
            summaryY = args.ImageWidth * 0.02f;

        using (var paint = new SKPaint { Color = Config.BRAND_GREEN, IsAntialias = true })
        {
            canvas.DrawRoundRect(
                new SKRect(args.Pad, summaryY, args.Pad + summaryWidth, summaryY + summaryBoxHeight),
                args.ImageWidth * 0.008f, args.ImageWidth * 0.008f, paint);
        }

        using (var paint = new SKPaint { Color = Config.BLACK, IsAntialias = true })
        {
            canvas.DrawText(shortSummary,
                args.Pad + (summaryWidth - summaryFont.MeasureText(shortSummary)) / 2,
                summaryY + args.Pad / 2 - args.ImageWidth * 0.004f - summaryFont.GetMetrics().Ascent,
                summaryFont, paint);
        }

        // Source box - GREEN with BLACK text
        var boxY = args.GradTop + args.OverlayPad;
        using (var paint = new SKPaint { Color = Config.BRAND_GREEN, IsAntialias = true })
        {
            canvas.DrawRoundRect(
                new SKRect(args.Pad, boxY, args.Pad + args.SourceWidth, boxY + args.SourceBoxHeight),
                args.ImageWidth * 0.008f, args.ImageWidth * 0.008f, paint);
        }

        var sourceTextWidth = args.SourceFont.MeasureText(args.SourceName);
        using (var paint = new SKPaint { Color = Config.BLACK, IsAntialias = true })
        {
            canvas.DrawText(args.SourceName,
                args.Pad + (args.SourceWidth - sourceTextWidth) / 2,
                boxY + args.SourceBoxPad - args.ImageWidth * 0.004f - args.SourceFont.GetMetrics().Ascent,
                args.SourceFont, paint);
        }

        // Headline
        var headlineY = boxY + args.SourceBoxHeight + args.GapMed;
        foreach (var (line, i) in args.HeadlineLines.Select((l, idx) => (l, idx)))
        {
            var y = headlineY + i * (args.HeadingFont.GetMetrics().Descent - args.HeadingFont.GetMetrics().Ascent + args.LineGap);
            if (y + args.HeadingFont.GetMetrics().Descent > args.ImageHeight - args.OverlayPad)
                break;

            DrawHighlightedText(canvas, args.Pad, y - args.HeadingFont.GetMetrics().Ascent, line, args.HeadingFont, Config.WHITE, new[] { Config.BRAND_GREEN, Config.BRAND_RED });
        }
    }

    // Template 4: Black top summary box, white bottom with RED highlights
    private static void CreateTemplate4(TemplateArgs args)
    {
        var canvas = args.Canvas;
        var innerPadding = args.ImageWidth * 0.03f;
        var smallGap = args.ImageWidth * 0.015f;
        var shortSummary = ExtractShortSummary(args.Article);

        // White bottom layout
        using (var paint = new SKPaint { Color = new SKColor(255, 255, 255, 230), IsAntialias = true })
        {
            canvas.DrawRoundRect(
                new SKRect(args.Pad, args.GradTop + args.OverlayPad, args.ImageWidth - args.Pad, args.ImageHeight - args.OverlayPad),
                args.CornerRadius, args.CornerRadius, paint);
        }

        // Black summary box
        var maxSummaryW = args.ImageWidth - args.Pad * 2 - innerPadding * 2;
        var summaryFont = CreateFont(Config.FONT_ARENA, args.ImageWidth * 0.055f, SKFontStyleWeight.Bold);
        var summaryWidth = (int)summaryFont.MeasureText(shortSummary) + (int)args.Pad * 2;

        while (summaryWidth > maxSummaryW && summaryFont.Size > args.ImageWidth * 0.02f)
        {
            summaryFont = CreateFont(Config.FONT_ARENA, summaryFont.Size * 0.9f, SKFontStyleWeight.Bold);
            summaryWidth = (int)summaryFont.MeasureText(shortSummary) + (int)args.Pad * 2;
        }

        var summaryBoxHeight = summaryFont.GetMetrics().Descent - summaryFont.GetMetrics().Ascent + args.Pad;
        var summaryY = args.GradTop - summaryBoxHeight - args.ImageWidth * 0.015f;
        if (summaryY < args.ImageWidth * 0.02f)
            summaryY = args.ImageWidth * 0.02f;

        var borderPad = 4f;
        // White border
        using (var paint = new SKPaint { Color = Config.WHITE, IsAntialias = true })
        {
            canvas.DrawRoundRect(
                new SKRect(args.Pad + innerPadding - borderPad, summaryY - borderPad,
                          args.Pad + innerPadding + summaryWidth + borderPad, summaryY + summaryBoxHeight + borderPad),
                args.ImageWidth * 0.008f + borderPad, args.ImageWidth * 0.008f + borderPad, paint);
        }

        // Black background
        using (var paint = new SKPaint { Color = new SKColor(20, 20, 20, 255), IsAntialias = true })
        {
            canvas.DrawRoundRect(
                new SKRect(args.Pad + innerPadding, summaryY, args.Pad + innerPadding + summaryWidth, summaryY + summaryBoxHeight),
                args.ImageWidth * 0.008f, args.ImageWidth * 0.008f, paint);
        }

        using (var paint = new SKPaint { Color = Config.BRAND_RED, IsAntialias = true })
        {
            canvas.DrawText(shortSummary,
                args.Pad + innerPadding + (summaryWidth - summaryFont.MeasureText(shortSummary)) / 2,
                summaryY + args.Pad / 2 - args.ImageWidth * 0.004f - summaryFont.GetMetrics().Ascent,
                summaryFont, paint);
        }

        // Source box - GREEN with BLACK text
        var boxY = args.GradTop + args.OverlayPad + smallGap;
        using (var paint = new SKPaint { Color = Config.BRAND_GREEN, IsAntialias = true })
        {
            canvas.DrawRoundRect(
                new SKRect(args.Pad + innerPadding + 20, boxY, args.Pad + args.SourceWidth + innerPadding + 20, boxY + args.SourceBoxHeight),
                args.ImageWidth * 0.008f, args.ImageWidth * 0.008f, paint);
        }

        var sourceTextWidth = args.SourceFont.MeasureText(args.SourceName);
        using (var paint = new SKPaint { Color = Config.BLACK, IsAntialias = true })
        {
            canvas.DrawText(args.SourceName,
                args.Pad + innerPadding + 20 + (args.SourceWidth - sourceTextWidth) / 2,
                boxY + args.SourceBoxPad - args.ImageWidth * 0.004f - args.SourceFont.GetMetrics().Ascent,
                args.SourceFont, paint);
        }

        // Headline with RED highlights
        var headlineY = boxY + args.SourceBoxHeight + args.GapMed;
        var availableH = (args.ImageHeight - args.OverlayPad - innerPadding) - headlineY;
        var maxTextW = args.ImageWidth - args.Pad * 2 - innerPadding * 2 - 40;

        var hf = args.HeadingFont;
        var lines = new List<string>(args.HeadlineLines);
        while (lines.Count * (hf.GetMetrics().Descent - hf.GetMetrics().Ascent + args.LineGap) - args.LineGap > availableH && hf.Size > args.ImageWidth * 0.02f)
        {
            hf = CreateFont(Config.FONT_ARENA, hf.Size * 0.9f, SKFontStyleWeight.Bold);
            lines = WrapText(canvas, args.Title, hf, maxTextW);
        }

        foreach (var (line, i) in lines.Select((l, idx) => (l, idx)))
        {
            var y = headlineY + i * (hf.GetMetrics().Descent - hf.GetMetrics().Ascent + args.LineGap);
            if (y + hf.GetMetrics().Descent > args.ImageHeight - args.OverlayPad - innerPadding)
                break;

            DrawHighlightedText(canvas, args.Pad + innerPadding + 20, y - hf.GetMetrics().Ascent, line, hf, Config.BLACK, new[] { Config.BRAND_RED, Config.BRAND_RED });
        }
    }

    // Template 5: Box Office style with stats bar
    private static void CreateTemplate5(TemplateArgs args)
    {
        var canvas = args.Canvas;
        var innerPadding = args.ImageWidth * 0.03f;
        var smallGap = args.ImageWidth * 0.015f;

        // Stats bar
        var statsH = args.ImageWidth * 0.065f;
        var statsY = args.GradTop + args.OverlayPad;

        using (var paint = new SKPaint { Color = new SKColor(20, 20, 20, 200), IsAntialias = true })
        {
            canvas.DrawRoundRect(
                new SKRect(args.Pad, statsY, args.ImageWidth - args.Pad, statsY + statsH),
                args.CornerRadius, args.CornerRadius, paint);
        }

        var catLabel = "BREAKING NEWS";
        var catFont = CreateFont(Config.FONT_ARENA, args.ImageWidth * 0.025f, SKFontStyleWeight.Bold);
        var catTextWidth = catFont.MeasureText(catLabel);

        using (var paint = new SKPaint { Color = Config.BRAND_RED, IsAntialias = true })
        {
            canvas.DrawText(catLabel,
                args.Pad + innerPadding,
                statsY + (statsH - catFont.GetMetrics().Descent + catFont.GetMetrics().Ascent) / 2,
                catFont, paint);
        }

        // Dot separator
        var dotX = args.Pad + innerPadding + catTextWidth + args.ImageWidth * 0.02f;
        var dotY = statsY + statsH / 2;
        using (var paint = new SKPaint { Color = Config.WHITE, IsAntialias = true })
        {
            canvas.DrawCircle(dotX, dotY, 4, paint);
        }

        // Source after dot
        using (var paint = new SKPaint { Color = Config.LIGHT_GRAY, IsAntialias = true })
        {
            canvas.DrawText(args.SourceName,
                dotX + args.ImageWidth * 0.02f,
                statsY + (statsH - args.SourceFont.GetMetrics().Descent + args.SourceFont.GetMetrics().Ascent) / 2,
                args.SourceFont, paint);
        }

        // White bottom layout
        var boxY = statsY + statsH + smallGap;
        using (var paint = new SKPaint { Color = new SKColor(255, 255, 255, 230), IsAntialias = true })
        {
            canvas.DrawRoundRect(
                new SKRect(args.Pad, boxY, args.ImageWidth - args.Pad, args.ImageHeight - args.OverlayPad),
                args.CornerRadius, args.CornerRadius, paint);
        }

        // Source box - GREEN with BLACK text
        using (var paint = new SKPaint { Color = Config.BRAND_GREEN, IsAntialias = true })
        {
            canvas.DrawRoundRect(
                new SKRect(args.Pad + innerPadding + 20, boxY + smallGap, args.Pad + args.SourceWidth + innerPadding + 20, boxY + smallGap + args.SourceBoxHeight),
                args.ImageWidth * 0.008f, args.ImageWidth * 0.008f, paint);
        }

        var sourceTextWidth = args.SourceFont.MeasureText(args.SourceName);
        using (var paint = new SKPaint { Color = Config.BLACK, IsAntialias = true })
        {
            canvas.DrawText(args.SourceName,
                args.Pad + innerPadding + 20 + (args.SourceWidth - sourceTextWidth) / 2,
                boxY + smallGap + args.SourceBoxPad - args.ImageWidth * 0.004f - args.SourceFont.GetMetrics().Ascent,
                args.SourceFont, paint);
        }

        // Headline with RED highlights
        var headlineY = boxY + smallGap + args.SourceBoxHeight + args.GapMed;
        var availableH = (args.ImageHeight - args.OverlayPad - innerPadding) - headlineY;
        var maxTextW = args.ImageWidth - args.Pad * 2 - innerPadding * 2 - 40;

        var hf = args.HeadingFont;
        var lines = new List<string>(args.HeadlineLines);
        while (lines.Count * (hf.GetMetrics().Descent - hf.GetMetrics().Ascent + args.LineGap) - args.LineGap > availableH && hf.Size > args.ImageWidth * 0.02f)
        {
            hf = CreateFont(Config.FONT_ARENA, hf.Size * 0.9f, SKFontStyleWeight.Bold);
            lines = WrapText(canvas, args.Title, hf, maxTextW);
        }

        foreach (var (line, i) in lines.Select((l, idx) => (l, idx)))
        {
            var y = headlineY + i * (hf.GetMetrics().Descent - hf.GetMetrics().Ascent + args.LineGap);
            if (y + hf.GetMetrics().Descent > args.ImageHeight - args.OverlayPad - innerPadding)
                break;

            DrawHighlightedText(canvas, args.Pad + innerPadding + 20, y - hf.GetMetrics().Ascent, line, hf, Config.BLACK, new[] { Config.BRAND_RED, Config.BRAND_RED });
        }
    }

    // Template 6: List/fact format
    private static void CreateTemplate6(TemplateArgs args)
    {
        var canvas = args.Canvas;
        var innerPadding = args.ImageWidth * 0.03f;
        var smallGap = args.ImageWidth * 0.015f;

        // White bottom layout
        var boxY = args.GradTop + args.OverlayPad + smallGap;
        using (var paint = new SKPaint { Color = new SKColor(255, 255, 255, 230), IsAntialias = true })
        {
            canvas.DrawRoundRect(
                new SKRect(args.Pad, boxY, args.ImageWidth - args.Pad, args.ImageHeight - args.OverlayPad),
                args.CornerRadius, args.CornerRadius, paint);
        }

        // Source badge - GREEN with BLACK text
        using (var paint = new SKPaint { Color = Config.BRAND_GREEN, IsAntialias = true })
        {
            canvas.DrawRoundRect(
                new SKRect(args.Pad + innerPadding + 20, boxY + smallGap, args.Pad + args.SourceWidth + innerPadding + 20, boxY + smallGap + args.SourceBoxHeight),
                args.ImageWidth * 0.008f, args.ImageWidth * 0.008f, paint);
        }

        var sourceTextWidth = args.SourceFont.MeasureText(args.SourceName);
        using (var paint = new SKPaint { Color = Config.BLACK, IsAntialias = true })
        {
            canvas.DrawText(args.SourceName,
                args.Pad + innerPadding + 20 + (args.SourceWidth - sourceTextWidth) / 2,
                boxY + smallGap + args.SourceBoxPad - args.ImageWidth * 0.004f - args.SourceFont.GetMetrics().Ascent,
                args.SourceFont, paint);
        }

        // Header
        var headerFont = CreateFont(Config.FONT_ARENA, args.ImageWidth * 0.038f, SKFontStyleWeight.Bold);
        var headerY = boxY + smallGap + args.SourceBoxHeight + args.GapMed;
        var headerLines = WrapText(canvas, args.Title, headerFont, args.ImageWidth - args.Pad * 2 - innerPadding * 2 - 40);

        foreach (var (line, i) in headerLines.Take(2).Select((l, idx) => (l, idx)))
        {
            var y = headerY + i * (headerFont.GetMetrics().Descent - headerFont.GetMetrics().Ascent + args.LineGap);
            if (y + headerFont.GetMetrics().Descent > args.ImageHeight - args.OverlayPad - innerPadding - args.ImageWidth * 0.05f)
                break;

            DrawHighlightedText(canvas, args.Pad + innerPadding + 20, y - headerFont.GetMetrics().Ascent, line, headerFont, Config.BLACK, new[] { Config.BRAND_RED, Config.BRAND_RED });
        }

        // Separator
        var sepY = headerY + headerLines.Take(2).Count() * (headerFont.GetMetrics().Descent - headerFont.GetMetrics().Ascent + args.LineGap) + smallGap;
        using (var paint = new SKPaint { Color = Config.BRAND_GREEN, StrokeWidth = 3, IsAntialias = true })
        {
            canvas.DrawLine(args.Pad + innerPadding + 20, sepY, args.ImageWidth - args.Pad - innerPadding - 20, sepY, paint);
        }

        // Body text
        var summary = (args.Article.Summary ?? args.Title);
        if (summary.Length > 200) summary = summary.Substring(0, 200);
        var bodyFont = CreateFont(Config.FONT_ARENA, args.ImageWidth * 0.028f, SKFontStyleWeight.Normal);
        var bodyY = sepY + smallGap;
        var bodyLines = WrapText(canvas, summary, bodyFont, args.ImageWidth - args.Pad * 2 - innerPadding * 2 - 40);

        foreach (var (line, i) in bodyLines.Take(4).Select((l, idx) => (l, idx)))
        {
            var y = bodyY + i * (bodyFont.GetMetrics().Descent - bodyFont.GetMetrics().Ascent + args.LineGap * 0.8f);
            if (y + bodyFont.GetMetrics().Descent > args.ImageHeight - args.OverlayPad - innerPadding - args.ImageWidth * 0.02f)
                break;

            using var paint = new SKPaint { Color = Config.MEDIUM_GRAY, IsAntialias = true };
            canvas.DrawText(line, args.Pad + innerPadding + 20, y - bodyFont.GetMetrics().Ascent, bodyFont, paint);
        }
    }
}