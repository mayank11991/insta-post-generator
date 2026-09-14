using System.Diagnostics;
using Android.Content;
using Android.App;

namespace InstaPostGenerator.Services;

public static class VideoDownloader
{
    public static async Task<string> DownloadVideoAsync(string videoUrl, string outputDir)
    {
        try
        {
            Directory.CreateDirectory(outputDir);

            // Try direct yt-dlp first
            var result = await DownloadDirectAsync(videoUrl, outputDir);
            if (!string.IsNullOrEmpty(result) && File.Exists(result))
                return result;

            // Try Termux RUN_COMMAND
            result = await DownloadViaTermuxAsync(videoUrl, outputDir);
            if (!string.IsNullOrEmpty(result) && File.Exists(result))
                return result;

            Debug.WriteLine("[VideoDownloader] No download method available");
            return null;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[VideoDownloader] Error: {ex.Message}");
            return null;
        }
    }

    private static async Task<string> DownloadDirectAsync(string videoUrl, string outputDir)
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
                return outputPath;

            return null;
        }
        catch
        {
            return null;
        }
    }

    private static async Task<string> DownloadViaTermuxAsync(string videoUrl, string outputDir)
    {
#if ANDROID
        try
        {
            var filename = $"{Guid.NewGuid()}.mp4";
            var outputPath = Path.Combine(outputDir, filename);
            var doneFile = outputPath + ".done";
            var logFile = outputPath + ".log";

            // Write shell script
            var script = $"#!/data/data/com.termux/files/usr/bin/bash\n" +
                        $"echo \"Starting\" > \"{logFile}\"\n" +
                        $"yt-dlp -f \"bestvideo[ext=mp4]+bestaudio[ext=m4a]/best[ext=mp4]/best\" " +
                        $"--merge-output-format mp4 " +
                        $"--download-sections \"*0-60\" " +
                        $"-o \"{outputPath}\" " +
                        $"--no-playlist " +
                        $"--socket-timeout 30 " +
                        $"\"{videoUrl}\" 2>>\"{logFile}\"\n" +
                        $"echo $? > \"{doneFile}\"\n";

            var dir = "/sdcard/Download/insta-post";
            Directory.CreateDirectory(dir);
            var scriptPath = Path.Combine(dir, "dl_reel.sh");
            File.WriteAllText(scriptPath, script);

            // Send broadcast to Termux
            var intent = new Intent("com.termux.RUN_COMMAND");
            intent.SetPackage("com.termux");
            intent.PutExtra("com.termux.RUN_COMMAND_PATH", "/data/data/com.termux/files/usr/bin/bash");
            intent.PutExtra("com.termux.RUN_COMMAND_ARGUMENTS", new[] { scriptPath });
            intent.PutExtra("com.termux.RUN_COMMAND_WORK_DIRECTORY", dir);

            Android.App.Application.Context.SendBroadcast(intent);
            Debug.WriteLine("[VideoDownloader] Termux broadcast sent");

            // Poll for completion (max 3 minutes)
            for (int i = 0; i < 90; i++)
            {
                await Task.Delay(2000);

                if (File.Exists(doneFile))
                {
                    var exitCode = (await File.ReadAllTextAsync(doneFile)).Trim();
                    File.Delete(doneFile);

                    if (exitCode == "0" && File.Exists(outputPath))
                    {
                        var info = new FileInfo(outputPath);
                        if (info.Length > 10000)
                        {
                            File.Delete(logFile);
                            File.Delete(scriptPath);
                            Debug.WriteLine($"[VideoDownloader] Success: {info.Length} bytes");
                            return outputPath;
                        }
                    }

                    var log = File.Exists(logFile) ? await File.ReadAllTextAsync(logFile) : "";
                    Debug.WriteLine($"[VideoDownloader] Failed: {log}");
                    File.Delete(logFile);
                    File.Delete(scriptPath);
                    return null;
                }
            }

            File.Delete(scriptPath);
            Debug.WriteLine("[VideoDownloader] Timed out");
            return null;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[VideoDownloader] Termux error: {ex.Message}");
            return null;
        }
#else
        return null;
#endif
    }
}
