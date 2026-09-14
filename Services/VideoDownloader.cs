using System.Diagnostics;

namespace InstaPostGenerator.Services;

public static class VideoDownloader
{
    private static readonly string LogFile = "/sdcard/Download/insta-post/dl_debug.log";

    private static void Log(string msg)
    {
        var line = $"{DateTime.Now:HH:mm:ss} {msg}";
        Debug.WriteLine($"[VideoDownloader] {msg}");
        try { File.AppendAllText(LogFile, line + "\n"); } catch { }
    }

    public static async Task<string> DownloadVideoAsync(string videoUrl, string outputDir)
    {
        try
        {
            Directory.CreateDirectory(outputDir);
            File.WriteAllText(LogFile, "");

            Log($"Starting download: {videoUrl}");

            var result = await DownloadViaTermuxAsync(videoUrl, outputDir);
            if (!string.IsNullOrEmpty(result) && File.Exists(result))
                return result;

            Log("Termux download failed");
            return null;
        }
        catch (Exception ex)
        {
            Log($"Error: {ex.Message}");
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
                        $"RESULT=$?\n" +
                        $"echo $RESULT > \"{doneFile}\"\n" +
                        $"exit $RESULT\n";

            var dir = "/sdcard/Download/insta-post";
            Directory.CreateDirectory(dir);
            var scriptPath = Path.Combine(dir, $"dl_{id}.sh");
            File.WriteAllText(scriptPath, script);
            Log($"Script: {scriptPath}");

            var intent = new Android.Content.Intent("com.termux.RUN_COMMAND");
            intent.SetClassName("com.termux", "com.termux.app.RunCommandService");
            intent.PutExtra("com.termux.RUN_COMMAND_PATH", "/data/data/com.termux/files/usr/bin/bash");
            intent.PutExtra("com.termux.RUN_COMMAND_ARGUMENTS", new[] { scriptPath });
            intent.PutExtra("com.termux.RUN_COMMAND_WORK_DIRECTORY", dir);
            intent.PutExtra("com.termux.RUN_COMMAND_BACKGROUND", true);

            try
            {
                Android.App.Application.Context.StartForegroundService(intent);
                Log("ForegroundService started");
            }
            catch (Exception ex1)
            {
                Log($"ForegroundService failed: {ex1.Message}");
                try
                {
                    Android.App.Application.Context.StartService(intent);
                    Log("StartService started");
                }
                catch (Exception ex2)
                {
                    Log($"StartService failed: {ex2.Message}");
                    return null;
                }
            }

            for (int i = 0; i < 90; i++)
            {
                await Task.Delay(2000);

                if (i % 10 == 0)
                    Log($"Waiting... {i * 2}s");

                if (File.Exists(doneFile))
                {
                    var exitCode = (await File.ReadAllTextAsync(doneFile)).Trim();
                    File.Delete(doneFile);

                    if (exitCode == "0" && File.Exists(outputPath))
                    {
                        var fi = new FileInfo(outputPath);
                        if (fi.Length > 10000)
                        {
                            var log = File.Exists(logFile) ? await File.ReadAllTextAsync(logFile) : "";
                            File.Delete(logFile);
                            File.Delete(scriptPath);
                            Log($"SUCCESS: {fi.Length} bytes");
                            if (!string.IsNullOrWhiteSpace(log))
                                Log($"yt-dlp: {log[..Math.Min(200, log.Length)]}");
                            return outputPath;
                        }
                    }

                    var failLog = File.Exists(logFile) ? await File.ReadAllTextAsync(logFile) : "empty";
                    File.Delete(logFile);
                    File.Delete(scriptPath);
                    Log($"yt-dlp FAILED (exit={exitCode}): {failLog[..Math.Min(300, failLog.Length)]}");
                    return null;
                }
            }

            File.Delete(scriptPath);
            Log("Timed out after 180s");
            return null;
        }
        catch (Exception ex)
        {
            Log($"Termux error: {ex.Message}");
            return null;
        }
#else
        return null;
#endif
    }
}
