using System.Diagnostics;

namespace InstaPostGenerator.Services;

public static class VideoDownloader
{
    public static async Task<string> DownloadVideoAsync(string videoUrl, string outputDir)
    {
        try
        {
            Directory.CreateDirectory(outputDir);
            var outputPath = Path.Combine(outputDir, $"{Guid.NewGuid()}.mp4");

            // Try yt-dlp first (most reliable)
            if (await IsYtDlpAvailable())
            {
                return await DownloadWithYtDlpAsync(videoUrl, outputPath);
            }

            Debug.WriteLine("[VideoDownloader] yt-dlp not found. Install via Termux: pkg install yt-dlp");
            return null;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[VideoDownloader] Error: {ex.Message}");
            return null;
        }
    }

    private static async Task<bool> IsYtDlpAvailable()
    {
        // Check standard paths including Termux
        var paths = new[] { "yt-dlp", "/data/data/com.termux/files/usr/bin/yt-dlp" };
        foreach (var path in paths)
        {
            try
            {
                var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = path,
                        Arguments = "--version",
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    }
                };

                process.Start();
                await process.WaitForExitAsync();
                if (process.ExitCode == 0)
                {
                    _ytDlpPath = path;
                    return true;
                }
            }
            catch { }
        }
        return false;
    }

    private static string _ytDlpPath = "yt-dlp";

    private static async Task<string> DownloadWithYtDlpAsync(string videoUrl, string outputPath)
    {
        try
        {
            // Download best quality MP4, max 60 seconds
            var arguments = $"-f \"bestvideo[ext=mp4]+bestaudio[ext=m4a]/best[ext=mp4]/best\"" +
                          $" --merge-output-format mp4" +
                          $" --download-sections \"*0-60\"" +
                          $" -o \"{outputPath}\"" +
                          $" --no-playlist" +
                          $" --socket-timeout 30" +
                          $" \"{videoUrl}\"";

            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = _ytDlpPath,
                    Arguments = arguments,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            process.Start();
            var stdout = await process.StandardOutput.ReadToEndAsync();
            var stderr = await process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync();

            if (process.ExitCode == 0 && File.Exists(outputPath))
            {
                Debug.WriteLine($"[VideoDownloader] yt-dlp download OK: {outputPath}");
                return outputPath;
            }

            Debug.WriteLine($"[VideoDownloader] yt-dlp failed: {stderr}");
            return null;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[VideoDownloader] yt-dlp error: {ex.Message}");
            return null;
        }
    }

    private static async Task<string> DownloadDirectAsync(string videoUrl, string outputPath)
    {
        try
        {
            using var httpClient = new HttpClient();
            httpClient.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0");

            var response = await httpClient.GetAsync(videoUrl);
            response.EnsureSuccessStatusCode();

            await using var stream = await response.Content.ReadAsStreamAsync();
            await using var fileStream = File.Create(outputPath);
            await stream.CopyToAsync(fileStream);

            if (File.Exists(outputPath))
            {
                Debug.WriteLine($"[VideoDownloader] Direct download OK: {outputPath}");
                return outputPath;
            }

            return null;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[VideoDownloader] Direct download error: {ex.Message}");
            return null;
        }
    }

    public static async Task<string> ExtractAudioAsync(string videoPath)
    {
        try
        {
            var audioPath = Path.ChangeExtension(videoPath, ".mp3");

            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "ffmpeg",
                    Arguments = $"-i \"{videoPath}\" -vn -acodec libmp3lame -q:a 2 \"{audioPath}\" -y",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            process.Start();
            await process.WaitForExitAsync();

            if (process.ExitCode == 0 && File.Exists(audioPath))
            {
                Debug.WriteLine($"[VideoDownloader] Audio extraction OK: {audioPath}");
                return audioPath;
            }

            return null;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[VideoDownloader] Audio extraction error: {ex.Message}");
            return null;
        }
    }
}