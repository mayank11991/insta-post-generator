using System.Diagnostics;

namespace InstaPostGenerator.Services;

public static class VideoDownloader
{
    public static async Task<string> DownloadVideoAsync(string videoUrl, string outputDir)
    {
        try
        {
            Directory.CreateDirectory(outputDir);

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

            Debug.WriteLine($"[VideoDownloader] Script: {scriptPath}");
            Debug.WriteLine($"[VideoDownloader] Output: {outputPath}");

            // Use am start-foreground-service via shell
            var args = $"am start-foreground-service " +
                       $"-n com.termux/.app.RunCommandService " +
                       $"--es com.termux.RUN_COMMAND_PATH /data/data/com.termux/files/usr/bin/bash " +
                       $"--esa com.termux.RUN_COMMAND_ARGUMENTS {scriptPath} " +
                       $"--es com.termux.RUN_COMMAND_WORK_DIRECTORY {dir}";

            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "/system/bin/sh",
                    Arguments = $"-c \"{args.Replace("\"", "\\\"")}\"",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            process.Start();
            var output = await process.StandardOutput.ReadToEndAsync();
            var err = await process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync();
            Debug.WriteLine($"[VideoDownloader] am result: {output} err: {err}");

            // Poll for completion (max 2 minutes)
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
            Debug.WriteLine("[VideoDownloader] Timed out after 120s");
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
