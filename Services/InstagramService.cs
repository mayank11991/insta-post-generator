using System.Text;
using System.Text.Json;
using InstaPostGenerator.Models;

namespace InstaPostGenerator.Services;

public static class InstagramService
{
    private static readonly HttpClient _http = new();

    public static async Task<string> PostToInstagramAsync(string imagePath, string caption, string hashtags, Action<string>? onStatus = null)
    {
        var accessToken = Config.META_ACCESS_TOKEN;
        var igUserId = Config.INSTAGRAM_BUSINESS_ACCOUNT_ID;

        if (string.IsNullOrEmpty(accessToken) || string.IsNullOrEmpty(igUserId))
            return "Error: Meta API credentials not configured";

        onStatus?.Invoke("Uploading image...");
        var imageUrl = await UploadToImgbbAsync(imagePath);
        if (string.IsNullOrEmpty(imageUrl))
            return "Error: Failed to upload image";

        onStatus?.Invoke("Creating media container...");
        var fullCaption = caption + "\n\n" + hashtags;
        var containerId = await CreateMediaContainerAsync(igUserId, imageUrl, fullCaption, accessToken);
        if (string.IsNullOrEmpty(containerId))
            return "Error: Failed to create media container";

        onStatus?.Invoke("Waiting for processing...");
        var ready = await WaitForContainerAsync(igUserId, containerId, accessToken);
        if (!ready)
            return "Error: Container processing failed or timed out";

        onStatus?.Invoke("Publishing to Instagram...");
        var mediaId = await PublishContainerAsync(igUserId, containerId, accessToken);
        if (string.IsNullOrEmpty(mediaId))
            return "Error: Failed to publish";

        return "Posted! Media ID: " + mediaId;
    }

    private static async Task<string> UploadToImgbbAsync(string imagePath)
    {
        try
        {
            var imageBytes = await File.ReadAllBytesAsync(imagePath);
            var base64 = Convert.ToBase64String(imageBytes);

            var content = new MultipartFormDataContent();
            content.Add(new StringContent(Config.IMGBB_API_KEY), "key");
            content.Add(new StringContent(base64), "image");

            var response = await _http.PostAsync("https://api.imgbb.com/1/upload", content);
            var json = await response.Content.ReadAsStringAsync();
            var doc = JsonDocument.Parse(json);

            if (doc.RootElement.TryGetProperty("data", out var data) &&
                data.TryGetProperty("url", out var url))
            {
                return url.GetString();
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine("[IG] Upload failed: " + ex.Message);
        }
        return null;
    }

    private static async Task<string> CreateMediaContainerAsync(string igUserId, string imageUrl, string caption, string accessToken)
    {
        try
        {
            var url = "https://graph.facebook.com/v19.0/" + igUserId + "/media";
            var payload = new
            {
                image_url = imageUrl,
                caption = caption,
                access_token = accessToken,
                share_to_facebook = true
            };

            var json = JsonSerializer.Serialize(payload);
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            var response = await _http.PostAsync(url, content);
            var responseJson = await response.Content.ReadAsStringAsync();
            var doc = JsonDocument.Parse(responseJson);

            if (doc.RootElement.TryGetProperty("id", out var id))
            {
                return id.GetString();
            }

            System.Diagnostics.Debug.WriteLine("[IG] Container creation failed: " + responseJson);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine("[IG] Container error: " + ex.Message);
        }
        return null;
    }

    private static async Task<bool> WaitForContainerAsync(string igUserId, string containerId, string accessToken, int maxWaitSeconds = 60)
    {
        for (int i = 0; i < maxWaitSeconds; i += 3)
        {
            try
            {
                var url = "https://graph.facebook.com/v19.0/" + containerId + "?fields=status_code&access_token=" + accessToken;
                var response = await _http.GetStringAsync(url);
                var doc = JsonDocument.Parse(response);

                if (doc.RootElement.TryGetProperty("status_code", out var status))
                {
                    var statusStr = status.GetString();
                    if (statusStr == "FINISHED")
                        return true;
                    if (statusStr == "ERROR")
                    {
                        System.Diagnostics.Debug.WriteLine("[IG] Container processing error");
                        return false;
                    }
                }
            }
            catch { }

            await Task.Delay(3000);
        }
        return false;
    }

    private static async Task<string> PublishContainerAsync(string igUserId, string containerId, string accessToken)
    {
        try
        {
            var url = "https://graph.facebook.com/v19.0/" + igUserId + "/media_publish";
            var payload = new
            {
                creation_id = containerId,
                access_token = accessToken
            };

            var json = JsonSerializer.Serialize(payload);
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            var response = await _http.PostAsync(url, content);
            var responseJson = await response.Content.ReadAsStringAsync();
            var doc = JsonDocument.Parse(responseJson);

            if (doc.RootElement.TryGetProperty("id", out var id))
            {
                return id.GetString();
            }

            System.Diagnostics.Debug.WriteLine("[IG] Publish failed: " + responseJson);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine("[IG] Publish error: " + ex.Message);
        }
        return null;
    }
}
