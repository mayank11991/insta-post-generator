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

        onStatus?.Invoke("Creating media container...");
        var fullCaption = caption + "\n\n" + hashtags;

        // Step 1: Upload image directly to Instagram via multipart form
        var containerId = await CreateMediaContainerDirectAsync(igUserId, imagePath, fullCaption, accessToken);
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

    private static async Task<string> CreateMediaContainerDirectAsync(string igUserId, string imagePath, string caption, string accessToken)
    {
        try
        {
            var url = "https://graph.facebook.com/v19.0/" + igUserId + "/media";

            var imageBytes = await File.ReadAllBytesAsync(imagePath);
            var imageFileName = Path.GetFileName(imagePath);

            var content = new MultipartFormDataContent();
            content.Add(new ByteArrayContent(imageBytes), "image", imageFileName);
            content.Add(new StringContent(caption), "caption");
            content.Add(new StringContent(accessToken), "access_token");
            content.Add(new StringContent("true"), "share_to_facebook");

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

    private static async Task<bool> WaitForContainerAsync(string igUserId, string containerId, string accessToken, int maxWaitSeconds = 120)
    {
        for (int i = 0; i < maxWaitSeconds; i += 5)
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

            await Task.Delay(5000);
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
