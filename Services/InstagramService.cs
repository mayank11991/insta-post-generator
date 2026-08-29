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

        // Step 1: Upload to Facebook Page to get a permanent URL
        var imageUrl = await UploadToFacebookPageAsync(imagePath, accessToken);
        if (string.IsNullOrEmpty(imageUrl))
        {
            // Fallback: try IMGBB
            imageUrl = await UploadToImgbbAsync(imagePath);
        }
        if (string.IsNullOrEmpty(imageUrl))
            return "Error: Failed to upload image";

        onStatus?.Invoke("Creating Instagram post...");
        var fullCaption = caption + "\n\n" + hashtags;

        // Step 2: Create IG media container
        var containerId = await CreateIgContainerAsync(igUserId, imageUrl, fullCaption, accessToken);
        if (string.IsNullOrEmpty(containerId))
            return "Error: Failed to create media container";

        onStatus?.Invoke("Waiting for processing...");
        var ready = await WaitForContainerAsync(containerId, accessToken);
        if (!ready)
            return "Error: Processing timed out";

        onStatus?.Invoke("Publishing...");
        var mediaId = await PublishAsync(igUserId, containerId, accessToken);
        if (string.IsNullOrEmpty(mediaId))
            return "Error: Failed to publish";

        return "Posted! Media ID: " + mediaId;
    }

    private static async Task<string> UploadToFacebookPageAsync(string imagePath, string accessToken)
    {
        try
        {
            // Get connected Facebook Page
            var accountsUrl = "https://graph.facebook.com/v19.0/me/accounts?access_token=" + accessToken;
            var accountsResp = await _http.GetStringAsync(accountsUrl);
            var accountsDoc = JsonDocument.Parse(accountsResp);

            if (!accountsDoc.RootElement.TryGetProperty("data", out var pages) || pages.GetArrayLength() == 0)
            {
                System.Diagnostics.Debug.WriteLine("[IG] No Facebook Pages found");
                return null;
            }

            var pageId = pages[0].GetProperty("id").GetString();
            var pageToken = pages[0].GetProperty("access_token").GetString();

            // Upload photo to Page (binary upload works here)
            var uploadUrl = "https://graph.facebook.com/v19.0/" + pageId + "/photos";
            var imageBytes = await File.ReadAllBytesAsync(imagePath);

            var content = new MultipartFormDataContent();
            content.Add(new ByteArrayContent(imageBytes), "source", Path.GetFileName(imagePath));
            content.Add(new StringContent("false"), "published");
            content.Add(new StringContent(pageToken), "access_token");

            var response = await _http.PostAsync(uploadUrl, content);
            var responseJson = await response.Content.ReadAsStringAsync();
            var doc = JsonDocument.Parse(responseJson);

            if (doc.RootElement.TryGetProperty("id", out var photoId))
            {
                var pid = photoId.GetString();
                // Get the image URL from the photo
                var infoUrl = "https://graph.facebook.com/v19.0/" + pid + "?fields=images{source}&access_token=" + pageToken;
                var infoResp = await _http.GetStringAsync(infoUrl);
                var infoDoc = JsonDocument.Parse(infoResp);

                if (infoDoc.RootElement.TryGetProperty("images", out var images) && images.GetArrayLength() > 0)
                {
                    var imgUrl = images[0].GetProperty("source").GetString();
                    System.Diagnostics.Debug.WriteLine("[IG] Facebook upload OK: " + imgUrl);
                    return imgUrl;
                }
            }

            System.Diagnostics.Debug.WriteLine("[IG] FB upload response: " + responseJson);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine("[IG] FB upload failed: " + ex.Message);
        }
        return null;
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
                var imgUrl = url.GetString();
                System.Diagnostics.Debug.WriteLine("[IG] IMGBB upload OK: " + imgUrl);
                return imgUrl;
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine("[IG] IMGBB failed: " + ex.Message);
        }
        return null;
    }

    private static async Task<string> CreateIgContainerAsync(string igUserId, string imageUrl, string caption, string accessToken)
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
                return id.GetString();

            System.Diagnostics.Debug.WriteLine("[IG] Container failed: " + responseJson);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine("[IG] Container error: " + ex.Message);
        }
        return null;
    }

    private static async Task<bool> WaitForContainerAsync(string containerId, string accessToken, int maxWait = 120)
    {
        for (int i = 0; i < maxWait; i += 5)
        {
            try
            {
                var url = "https://graph.facebook.com/v19.0/" + containerId + "?fields=status_code&access_token=" + accessToken;
                var response = await _http.GetStringAsync(url);
                var doc = JsonDocument.Parse(response);

                if (doc.RootElement.TryGetProperty("status_code", out var status))
                {
                    var s = status.GetString();
                    if (s == "FINISHED") return true;
                    if (s == "ERROR") return false;
                }
            }
            catch { }
            await Task.Delay(5000);
        }
        return false;
    }

    private static async Task<string> PublishAsync(string igUserId, string containerId, string accessToken)
    {
        try
        {
            var url = "https://graph.facebook.com/v19.0/" + igUserId + "/media_publish";
            var payload = new { creation_id = containerId, access_token = accessToken };
            var json = JsonSerializer.Serialize(payload);
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            var response = await _http.PostAsync(url, content);
            var responseJson = await response.Content.ReadAsStringAsync();
            var doc = JsonDocument.Parse(responseJson);

            if (doc.RootElement.TryGetProperty("id", out var id))
                return id.GetString();

            System.Diagnostics.Debug.WriteLine("[IG] Publish failed: " + responseJson);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine("[IG] Publish error: " + ex.Message);
        }
        return null;
    }
}
