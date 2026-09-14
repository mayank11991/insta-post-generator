using System.Diagnostics;

namespace InstaPostGenerator.Services;

public static class VideoDownloader
{
    public static async Task<string> DownloadVideoAsync(string videoUrl, string outputDir)
    {
        try
        {
            Directory.CreateDirectory(outputDir);
            return await DownloadViaTermuxAsync(videoUrl, outputDir);
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
            var id = Guid.NewGuid().ToString("N")[..8];
            var filename = $"{id}.mp4";
            var outputPath = Path.Combine(outputDir, filename);
            var doneFile = outputPath + ".done";
            var logFile = outputPath + ".log";

            var script = $"#!/data/data/com.termux/files/usr/bin/bash\n" +
                        $"echo start > \"{logFile}\"\n" +
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
            var scriptPath = Path.Combine(dir, $"dl_{id}.sh");
            File.WriteAllText(scriptPath, script);

            Debug.WriteLine($"[VideoDownloader] Script written: {scriptPath}");

            // Start Termux service using Android API
            var intent = new Android.Content.Intent("com.termux.RUN_COMMAND");
            intent.SetClassName("com.termux", "com.termux.app.RunCommandService");
            intent.PutExtra("com.termux.RUN_COMMAND_PATH", "/data/data/com.termux/files/usr/bin/bash");
            intent.PutExtra("com.termux.RUN_COMMAND_ARGUMENTS", new[] { scriptPath });
            intent.PutExtra("com.termux.RUN_COMMAND_WORK_DIRECTORY", dir);
            intent.PutExtra("com.termux.RUN_COMMAND_BACKGROUND", true);

            try
            {
                Android.App.Application.Context.StartForegroundService(intent);
                Debug.WriteLine("[VideoDownloader] ForegroundService started");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[VideoDownloader] ForegroundService failed: {ex.Message}");
                try
                {
                    Android.App.Application.Context.StartService(intent);
                    Debug.WriteLine("[VideoDownloader] StartService started");
                }
                catch (Exception ex2)
                {
                    Debug.WriteLine($"[VideoDownloader] StartService also failed: {ex2.Message}");
                }
            }

            // Poll for completion
            for (int i = 0; i < 60; i++)
            {
                await Task.Delay(2000);

                if (File.Exists(doneFile))
                {
                    var exitCode = (await File.ReadAllTextAsync(doneFile)).Trim();
                    File.Delete(doneFile);

                    if (exitCode == "0" && File.Exists(outputPath))
                    {
                        var fi = new FileInfo(outputPath);
                        if (fi.Length > 10000)
                        {
                            File.Delete(logFile);
                            File.Delete(scriptPath);
                            Debug.WriteLine($"[VideoDownloader] OK: {fi.Length} bytes");
                            return outputPath;
                        }
                    }

                    var log = File.Exists(logFile) ? await File.ReadAllTextAsync(logFile) : "";
                    Debug.WriteLine($"[VideoDownloader] Failed (exit={exitCode}): {log}");
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
            Debug.WriteLine($"[VideoDownloader] Error: {ex.Message}");
            return null;
        }
#else
        return null;
#endif
    }
}
