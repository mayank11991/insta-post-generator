using System.Diagnostics;

namespace InstaPostGenerator.Services;

public static class VideoProcessor
{
    public static async Task<string> TrimVideoAsync(string inputPath, int maxDurationSeconds = 60)
    {
        try
        {
            var outputPath = Path.ChangeExtension(inputPath, "_trimmed.mp4");

            // Get video duration first
            var duration = await GetVideoDurationAsync(inputPath);
            if (duration <= 0)
            {
                Debug.WriteLine("[VideoProcessor] Could not determine video duration");
                return inputPath; // Return original if we can't determine duration
            }

            if (duration <= maxDurationSeconds)
            {
                Debug.WriteLine($"[VideoProcessor] Video is {duration}s, within {maxDurationSeconds}s limit");
                return inputPath; // Already within limit
            }

            // Trim to max duration
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "ffmpeg",
                    Arguments = $"-i \"{inputPath}\" -t {maxDurationSeconds} -c copy \"{outputPath}\" -y",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            process.Start();
            await process.WaitForExitAsync();

            if (process.ExitCode == 0 && File.Exists(outputPath))
            {
                Debug.WriteLine($"[VideoProcessor] Trimmed video: {outputPath}");
                return outputPath;
            }

            Debug.WriteLine("[VideoProcessor] Trim failed, returning original");
            return inputPath;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[VideoProcessor] Trim error: {ex.Message}");
            return inputPath;
        }
    }

    public static async Task<string> ConvertToReelFormatAsync(string inputPath)
    {
        try
        {
            var outputPath = Path.ChangeExtension(inputPath, "_reel.mp4");

            // Blurred background + sharp foreground for 9:16 aspect ratio
            var filter = "[0:v]scale=1080:1920:force_original_aspect_ratio=increase,crop=1080:1920,boxblur=25:5[bg];" +
                         "[0:v]scale=1080:1920:force_original_aspect_ratio=decrease[fg];" +
                         "[bg][fg]overlay=(W-w)/2:(H-h)/2";

            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "ffmpeg",
                    Arguments = $"-i \"{inputPath}\"" +
                              $" -vf \"{filter}\"" +
                              $" -c:v libx264 -preset medium -crf 23" +
                              $" -c:a aac -b:a 128k" +
                              $" -movflags +faststart" +
                              $" \"{outputPath}\" -y",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            process.Start();
            var stderr = await process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync();

            if (process.ExitCode == 0 && File.Exists(outputPath))
            {
                Debug.WriteLine($"[VideoProcessor] Converted to reel format: {outputPath}");
                return outputPath;
            }

            Debug.WriteLine($"[VideoProcessor] Conversion failed: {stderr}");
            return inputPath;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[VideoProcessor] Conversion error: {ex.Message}");
            return inputPath;
        }
    }

    public static async Task<string> ExtractThumbnailAsync(string videoPath, int timestampSeconds = 0)
    {
        try
        {
            var thumbnailPath = Path.ChangeExtension(videoPath, "_thumb.jpg");

            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "ffmpeg",
                    Arguments = $"-i \"{videoPath}\"" +
                              $" -ss {timestampSeconds}" +
                              $" -vframes 1" +
                              $" -q:v 2" +
                              $" \"{thumbnailPath}\" -y",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            process.Start();
            await process.WaitForExitAsync();

            if (process.ExitCode == 0 && File.Exists(thumbnailPath))
            {
                Debug.WriteLine($"[VideoProcessor] Extracted thumbnail: {thumbnailPath}");
                return thumbnailPath;
            }

            return null;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[VideoProcessor] Thumbnail extraction error: {ex.Message}");
            return null;
        }
    }

    public static async Task<double> GetVideoDurationAsync(string videoPath)
    {
        try
        {
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "ffprobe",
                    Arguments = $"-v error -show_entries format=duration -of default=noprint_wrappers=1:nokey=1 \"{videoPath}\"",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            process.Start();
            var output = await process.StandardOutput.ReadToEndAsync();
            await process.WaitForExitAsync();

            if (double.TryParse(output.Trim(), out var duration))
            {
                return duration;
            }

            return 0;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[VideoProcessor] Duration check error: {ex.Message}");
            return 0;
        }
    }

    public static async Task<bool> IsFFmpegAvailableAsync()
    {
        try
        {
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "ffmpeg",
                    Arguments = "-version",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            process.Start();
            await process.WaitForExitAsync();
            return process.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }
}