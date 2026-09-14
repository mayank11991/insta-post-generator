using System.Diagnostics;

namespace InstaPostGenerator.Services;

public static class VideoDownloader
{
    private static readonly HttpClient _http = new();

    public static async Task<string> DownloadVideoAsync(string videoUrl, string outputDir)
    {
        try
        {
            Directory.CreateDirectory(outputDir);

            // Use Termux intent to run yt-dlp
            var result = await DownloadViaTermuxAsync(videoUrl, outputDir);
            if (!string.IsNullOrEmpty(result) && File.Exists(result))
                return result;

            Debug.WriteLine("[VideoDownloader] Download failed");
            return null;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[VideoDownloader] Error: {ex.Message}");
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
            var markerFile = Path.Combine(outputDir, $"{filename}.done");

            // Write download script to shared storage
            var scriptPath = Android.OS.Environment.GetExternalStoragePublicDirectory(
                Android.OS.Environment.DirectoryDownloads)?.AbsolutePath;
            if (string.IsNullOrEmpty(scriptPath)) return null;

            var scriptFile = Path.Combine(scriptPath, "insta-post", "dl_reel.sh");
            Directory.CreateDirectory(Path.GetDirectoryName(scriptFile)!);

            var script = $"#!/data/data/com.termux/files/usr/bin/bash\n" +
                         $"yt-dlp -f \"bestvideo[ext=mp4]+bestaudio[ext=m4a]/best[ext=mp4]/best\" " +
                         $"--merge-output-format mp4 " +
                         $"--download-sections \"*0-60\" " +
                         $"-o \"{outputPath}\" " +
                         $"--no-playlist " +
                         $"--socket-timeout 30 " +
                         $"\"{videoUrl}\"\n" +
                         $"echo done > \"{markerFile}\"\n";

            File.WriteAllText(scriptFile, script);
            Debug.WriteLine($"[VideoDownloader] Script written to: {scriptFile}");

            // Send Termux RUN_COMMAND intent using Android API
            var intent = new Android.Content.Intent("com.termux.RUN_COMMAND");
            intent.SetComponent(new Android.Content.ComponentName(
                "com.termux", "com.termux.app.RunCommandService"));
            intent.PutExtra("com.termux.RUN_COMMAND_PATH", "/data/data/com.termux/files/usr/bin/bash");
            intent.PutExtra("com.termux.RUN_COMMAND_ARGUMENTS", new[] { scriptFile });
            intent.PutExtra("com.termux.RUN_COMMAND_WORK_DIRECTORY", scriptPath);

            Android.App.Application.Context.StartService(intent);
            Debug.WriteLine("[VideoDownloader] Termux intent sent, waiting for download...");

            // Poll for completion
            for (int i = 0; i < 60; i++)
            {
                await Task.Delay(2000);

                if (File.Exists(markerFile))
                {
                    // Check if actual video file exists and is valid
                    if (File.Exists(outputPath))
                    {
                        var info = new FileInfo(outputPath);
                        if (info.Length > 10000)
                        {
                            File.Delete(markerFile);
                            File.Delete(scriptFile);
                            Debug.WriteLine($"[VideoDownloader] Download OK: {outputPath} ({info.Length} bytes)");
                            return outputPath;
                        }
                    }

                    // Marker exists but no valid video - download failed
                    File.Delete(markerFile);
                    File.Delete(scriptFile);
                    Debug.WriteLine("[VideoDownloader] Download failed (yt-dlp error)");
                    return null;
                }
            }

            File.Delete(scriptFile);
            Debug.WriteLine("[VideoDownloader] Download timed out");
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
