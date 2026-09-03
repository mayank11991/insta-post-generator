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

    // Layout constants for new template
    private const float PAD_RATIO = 0.04f;
    private const float CORNER_RADIUS_RATIO = 0.045f;

    public static void GenerateTestImage(string outputPath)
    {
        var bitmap = new SKBitmap(Config.IMAGE_WIDTH, Config.IMAGE_HEIGHT);
        using var canvas = new SKCanvas(bitmap);

        // Dark background
        canvas.Clear(new SKColor(0, 0, 0));
        
        // Draw blurred background (no image)
        DrawBlurredBackground(canvas, false);

        // Test template (branding is drawn inside CreateTemplate7)
        var headingFont = CreateFont(Config.FONT_ARENA, Config.IMAGE_WIDTH * 0.052f, SKFontStyleWeight.Bold);
        var sourceFont = CreateFont(Config.FONT_ARENA, Config.IMAGE_WIDTH * 0.022f, SKFontStyleWeight.SemiBold);
        var timestampFont = CreateFont(Config.FONT_ARENA, Config.IMAGE_WIDTH * 0.018f, SKFontStyleWeight.Normal);
        var brandFont = CreateFont(Config.FONT_ARENA, Config.IMAGE_WIDTH * 0.042f, SKFontStyleWeight.Bold);

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
            Pad = Config.IMAGE_WIDTH * PAD_RATIO,
            CornerRadius = Config.IMAGE_WIDTH * CORNER_RADIUS_RATIO,
            ImageWidth = Config.IMAGE_WIDTH,
            ImageHeight = Config.IMAGE_HEIGHT
        };

        CreateTemplate7(templateArgs);

        // Rounded corners
        ApplyRoundedCorners(bitmap, Config.IMAGE_WIDTH * 0.03f);

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

        // Create canvas
        SKBitmap canvasBitmap;
        if (articleBitmap != null)
        {
            canvasBitmap = new SKBitmap(Config.IMAGE_WIDTH, Config.IMAGE_HEIGHT);
            using (var canvas = new SKCanvas(canvasBitmap))
            {
                canvas.Clear(new SKColor(0, 0, 0));
                // Full-bleed background - fill entire canvas
                var scale = Math.Max((float)Config.IMAGE_WIDTH / articleBitmap.Width, (float)Config.IMAGE_HEIGHT / articleBitmap.Height);
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
                canvas.Clear(new SKColor(0, 0, 0));
            }
        }

        using var drawCanvas = new SKCanvas(canvasBitmap);

        // Draw heavy blur + dark overlay on background
        DrawBlurredBackground(drawCanvas, articleBitmap != null);

        // NOTE: Branding is now drawn inside CreateTemplate7 (top-left: 360buzz_)

        // Draw new template
        var headingFont = CreateFont(Config.FONT_ARENA, Config.IMAGE_WIDTH * 0.052f, SKFontStyleWeight.Bold);
        var sourceFont = CreateFont(Config.FONT_ARENA, Config.IMAGE_WIDTH * 0.022f, SKFontStyleWeight.SemiBold);
        var timestampFont = CreateFont(Config.FONT_ARENA, Config.IMAGE_WIDTH * 0.018f, SKFontStyleWeight.Normal);
        var brandFont = CreateFont(Config.FONT_ARENA, Config.IMAGE_WIDTH * 0.042f, SKFontStyleWeight.Bold);

        var sourceName = (article.Source?.Name ?? "Source").ToUpperInvariant();

        // Create template args for new layout
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
            Pad = Config.IMAGE_WIDTH * 0.04f,
            CornerRadius = Config.IMAGE_WIDTH * 0.045f,
            ImageWidth = Config.IMAGE_WIDTH,
            ImageHeight = Config.IMAGE_HEIGHT
        };

        CreateTemplate7(templateArgs);

        // Rounded corners on final image
        ApplyRoundedCorners(canvasBitmap, Config.IMAGE_WIDTH * 0.03f);

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

    // Heavy blur + dark overlay for background
    private static void DrawBlurredBackground(SKCanvas canvas, bool hasImage)
    {
        if (hasImage)
        {
            using (var shader = SKShader.CreateLinearGradient(
                new SKPoint(0, 0),
                new SKPoint(0, Config.IMAGE_HEIGHT),
                new[] { 
                    new SKColor(0, 0, 0, 220),
                    new SKColor(0, 0, 0, 150),
                    new SKColor(0, 0, 0, 220)
                },
                new float[] { 0f, 0.5f, 1f },
                SKShaderTileMode.Clamp))
            using (var paint = new SKPaint { Shader = shader })
            {
                canvas.DrawRect(new SKRect(0, 0, Config.IMAGE_WIDTH, Config.IMAGE_HEIGHT), paint);
            }
        }
        else
        {
            canvas.Clear(new SKColor(0, 0, 0));
        }
    }

    // Draw wireframe globe icon (top right)
    private static void DrawGlobeIcon(SKCanvas canvas, float x, float y, float size)
    {
        using var paint = new SKPaint 
        { 
            Color = Config.WHITE, 
            IsAntialias = true, 
            Style = SKPaintStyle.Stroke, 
            StrokeWidth = Math.Max(1.5f, size * 0.025f) 
        };
        
        var centerX = x + size / 2;
        var centerY = y + size / 2;
        var radius = size * 0.42f;
        
        canvas.DrawCircle(centerX, centerY, radius, paint);
        canvas.DrawLine(x + size * 0.08f, centerY, x + size * 0.92f, centerY, paint);
        
        using var path = new SKPath();
        path.AddArc(new SKRect(x + size * 0.08f, y + size * 0.12f, x + size * 0.92f, y + size * 0.88f), 0, 180);
        canvas.DrawPath(path, paint);
        
        path.Reset();
        path.AddArc(new SKRect(x + size * 0.2f, y + size * 0.05f, x + size * 0.8f, y + size * 0.95f), -30, 180);
        canvas.DrawPath(path, paint);
    }

    // New Template 7: Central Card with Photo Top, Red Wave Bottom
    private static void CreateTemplate7(TemplateArgs args)
    {
        var canvas = args.Canvas;
        var W = args.ImageWidth;
        var H = args.ImageHeight;
        var pad = args.Pad;
        var cardRadius = args.CornerRadius;
        
        var electricRed = new SKColor(0xE5, 0x00, 0x12);
        var deepBlack = new SKColor(0x00, 0x00, 0x00);
        var pureWhite = Config.WHITE;

        var cardMargin = W * 0.055f;
        var cardLeft = cardMargin;
        var cardRight = W - cardMargin;
        var cardWidth = cardRight - cardLeft;
        var cardTop = H * 0.09f;
        var cardBottom = H * 0.91f;
        var cardHeight = cardBottom - cardTop;
        
        var photoHeight = cardHeight * 0.55f;
        var photoBottom = cardTop + photoHeight;
        var textBlockTop = photoBottom - W * 0.025f;

        // Card shadow
        using (var shadowPaint = new SKPaint 
        { 
            Color = new SKColor(0, 0, 0, 120), 
            IsAntialias = true,
            ImageFilter = SKImageFilter.CreateBlur(20, 20)
        })
        {
            canvas.DrawRoundRect(
                new SKRect(cardLeft + 4, cardTop + 4, cardRight + 4, cardBottom + 4),
                cardRadius, cardRadius, shadowPaint);
        }

        // Card background
        using (var cardPaint = new SKPaint { Color = deepBlack, IsAntialias = true })
        {
            canvas.DrawRoundRect(
                new SKRect(cardLeft, cardTop, cardRight, cardBottom),
                cardRadius, cardRadius, cardPaint);
        }

        // Photo area clipping path (sharp top, rounded bottom)
        var photoLeft = cardLeft + W * 0.025f;
        var photoRight = cardRight - W * 0.025f;
        var photoTop = cardTop + W * 0.025f;
        var photoRadius = W * 0.02f;
        
        using (var photoPath = new SKPath())
        {
            photoPath.MoveTo(photoLeft, photoTop);
            photoPath.LineTo(photoRight, photoTop);
            photoPath.LineTo(photoRight, photoBottom - photoRadius);
            photoPath.ArcTo(new SKRect(photoRight - photoRadius * 2, photoBottom - photoRadius * 2, photoRight, photoBottom), 270, 90, false);
            photoPath.LineTo(photoLeft + photoRadius, photoBottom);
            photoPath.ArcTo(new SKRect(photoLeft, photoBottom - photoRadius * 2, photoLeft + photoRadius * 2, photoBottom), 180, 90, false);
            photoPath.Close();
            
            canvas.Save();
            canvas.ClipPath(photoPath, SKClipOperation.Intersect, true);
            
            using (var borderPaint = new SKPaint 
            { 
                Color = new SKColor(255, 255, 255, 30), 
                IsAntialias = true, 
                Style = SKPaintStyle.Stroke, 
                StrokeWidth = 1.5f 
            })
            {
                canvas.DrawPath(photoPath, borderPaint);
            }
            
            canvas.Restore();
        }

        // Red wave text block - SWEEPING WAVE (S-shape: top-right up, bottom-left down)
        using (var wavePath = new SKPath())
        {
            var waveAmplitude = W * 0.045f;  // Larger for visible sweep
            var waveStartX = cardLeft;
            var waveEndX = cardRight;
            var waveY = textBlockTop;
            
            // Start at left, go UP at right (top-right sweep up)
            wavePath.MoveTo(waveStartX, waveY + waveAmplitude * 0.3f);
            
            // First curve: sweep UP at right side
            var ctrlX1 = waveStartX + cardWidth * 0.35f;
            var ctrlY1 = waveY - waveAmplitude * 0.8f;   // Upward at top-right
            var ctrlX2 = waveStartX + cardWidth * 0.65f;
            var ctrlY2 = waveY + waveAmplitude * 1.2f;  // Down through middle
            var midX = waveStartX + cardWidth * 0.5f;
            var midY = waveY + waveAmplitude * 0.2f;
            
            wavePath.CubicTo(ctrlX1, ctrlY1, ctrlX2, ctrlY2, midX, midY);
            
            // Second curve: sweep DOWN at left side (bottom-left sweep down)
            ctrlX1 = midX + cardWidth * 0.15f;
            ctrlY1 = waveY - waveAmplitude * 0.3f;
            ctrlX2 = waveEndX - cardWidth * 0.35f;
            ctrlY2 = waveY + waveAmplitude * 2.0f;  // Deep down at bottom-left
            var endX = waveEndX;
            var endY = waveY + waveAmplitude * 0.5f;
            
            wavePath.CubicTo(ctrlX1, ctrlY1, ctrlX2, ctrlY2, endX, endY);
            
            // Close path to bottom of card
            wavePath.LineTo(waveEndX, cardBottom);
            wavePath.LineTo(waveStartX, cardBottom);
            wavePath.Close();
            
            using (var wavePaint = new SKPaint { Color = electricRed, IsAntialias = true })
            {
                canvas.DrawPath(wavePath, wavePaint);
            }
        }

        // Source badge (pill-shaped, INSIDE photo area at bottom-left, NOT touching wave)
        var badgeText = args.SourceName;
        var badgePaddingX = W * 0.03f;
        var badgePaddingY = W * 0.01f;
        var badgeTextWidth = args.SourceFont.MeasureText(badgeText);
        var badgeWidth = badgeTextWidth + badgePaddingX * 2;
        var badgeHeight = args.SourceFont.GetMetrics().Descent - args.SourceFont.GetMetrics().Ascent + badgePaddingY * 2;
        var badgeRadius = badgeHeight / 2;
        
        var badgeX = photoLeft + W * 0.02f;
        var badgeY = photoBottom - badgeHeight - W * 0.05f;  // Well inside photo area (5% gap from photo bottom)
        
        using (var badgePaint = new SKPaint { Color = electricRed, IsAntialias = true })
        {
            canvas.DrawRoundRect(
                new SKRect(badgeX, badgeY, badgeX + badgeWidth, badgeY + badgeHeight),
                badgeRadius, badgeRadius, badgePaint);
        }
        
        using (var badgeTextPaint = new SKPaint { Color = pureWhite, IsAntialias = true })
        {
            var textY = badgeY + badgePaddingY - args.SourceFont.GetMetrics().Ascent;
            canvas.DrawText(badgeText, badgeX + badgePaddingX, textY, args.SourceFont, badgeTextPaint);
        }

        // Top left: 360buzz_ branding - "360" in YELLOW (thicker stroke), "buzz_" in RED
        var brandX = pad;
        var brandY = pad + W * 0.02f;
        var brandFont = args.BrandFont;
        var brandMetrics = brandFont.GetMetrics();
        
        var text360 = "360";
        var textBuzz = "buzz_";
        var w360 = brandFont.MeasureText(text360);
        
        var strokeW = Math.Max(3, W * 0.004f);  // Thicker stroke for visibility
        
        // Draw "360" with thick black stroke then YELLOW fill
        using (var strokePaint = new SKPaint { Color = deepBlack, IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = strokeW })
        using (var fillPaint = new SKPaint { Color = Config.BRAND_YELLOW, IsAntialias = true })
        {
            canvas.DrawText(text360, brandX, brandY - brandMetrics.Ascent, brandFont, strokePaint);
            canvas.DrawText(text360, brandX, brandY - brandMetrics.Ascent, brandFont, fillPaint);
        }
        
        // Draw "buzz_" with thick black stroke then RED fill
        using (var strokePaint = new SKPaint { Color = deepBlack, IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = strokeW })
        using (var fillPaint = new SKPaint { Color = electricRed, IsAntialias = true })
        {
            canvas.DrawText(textBuzz, brandX + w360, brandY - brandMetrics.Ascent, brandFont, strokePaint);
            canvas.DrawText(textBuzz, brandX + w360, brandY - brandMetrics.Ascent, brandFont, fillPaint);
        }

        // Globe icon REMOVED per request

        // Main title text (left-aligned in red wave)
        var textPadding = W * 0.05f;
        var textLeft = cardLeft + textPadding;
        var textRight = cardRight - textPadding;
        var textMaxWidth = textRight - textLeft;
        var textTop = textBlockTop + W * 0.035f;
        
        var titleLines = WrapText(canvas, args.Title, args.HeadingFont, textMaxWidth);
        if (!titleLines.Any()) titleLines = new List<string> { args.Title };
        
        var availableTextHeight = cardBottom - textTop - W * 0.05f;
        var lineHeight = args.HeadingFont.GetMetrics().Descent - args.HeadingFont.GetMetrics().Ascent + W * 0.008f;
        
        while (titleLines.Count * lineHeight > availableTextHeight && args.HeadingFont.Size > W * 0.022f)
        {
            args.HeadingFont = CreateFont(Config.FONT_ARENA, args.HeadingFont.Size * 0.85f, SKFontStyleWeight.Bold);
            lineHeight = args.HeadingFont.GetMetrics().Descent - args.HeadingFont.GetMetrics().Ascent + W * 0.008f;
            titleLines = WrapText(canvas, args.Title, args.HeadingFont, textMaxWidth);
        }
        
        var currentY = textTop;
        foreach (var line in titleLines)
        {
            if (currentY + args.HeadingFont.GetMetrics().Descent > cardBottom - W * 0.02f)
                break;
                
            using var paint = new SKPaint { Color = pureWhite, IsAntialias = true };
            canvas.DrawText(line, textLeft, currentY - args.HeadingFont.GetMetrics().Ascent, args.HeadingFont, paint);
            currentY += lineHeight;
        }

        // Timestamp
        var timestamp = DateTime.Now.ToString("dd MMM yyyy \u2022 HH:mm");
        var tsGap = W * 0.018f;
        var tsY = currentY + tsGap;
        
        if (tsY + args.TimestampFont.GetMetrics().Descent < cardBottom - W * 0.015f)
        {
            using var tsPaint = new SKPaint { Color = new SKColor(255, 255, 255, 200), IsAntialias = true };
            canvas.DrawText(timestamp, textLeft, tsY - args.TimestampFont.GetMetrics().Ascent, args.TimestampFont, tsPaint);
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

    private static void ApplyRoundedCorners(SKBitmap bitmap, float radius)
    {
        // Using SKCanvas with clip path for rounded corners
        using var canvas = new SKCanvas(bitmap);
        using var path = new SKPath();
        path.AddRoundRect(new SKRect(0, 0, bitmap.Width, bitmap.Height), radius, radius);
        canvas.ClipPath(path, SKClipOperation.Intersect, true);
    }
}