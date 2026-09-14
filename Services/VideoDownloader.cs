using System.Diagnostics;

namespace InstaPostGenerator.Services;

public static class VideoDownloader
{
    public static async Task<string> DownloadVideoAsync(string videoUrl, string outputDir)
    {
        try
        {
            Directory.CreateDirectory(outputDir);

            // Try yt-dlp via local process
            var result = await DownloadWithYtDlpAsync(videoUrl, outputDir);
            if (!string.IsNullOrEmpty(result) && File.Exists(result))
                return result;

            Debug.WriteLine("[VideoDownloader] yt-dlp not available on device");
            return null;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[VideoDownloader] Error: {ex.Message}");
            return null;
        }
    }

    private static async Task<string> DownloadWithYtDlpAsync(string videoUrl, string outputDir)
    {
        try
        {
            var outputPath = Path.Combine(outputDir, $"{Guid.NewGuid()}.mp4");

            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "yt-dlp",
                    Arguments = $"-f \"bestvideo[ext=mp4]+bestaudio[ext=m4a]/best[ext=mp4]/best\"" +
                              $" --merge-output-format mp4" +
                              $" --download-sections \"*0-60\"" +
                              $" -o \"{outputPath}\"" +
                              $" --no-playlist" +
                              $" --socket-timeout 30" +
                              $" \"{videoUrl}\"",
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
                Debug.WriteLine($"[VideoDownloader] yt-dlp OK: {outputPath}");
                return outputPath;
            }

            return null;
        }
        catch
        {
            return null;
        }
    }
}
