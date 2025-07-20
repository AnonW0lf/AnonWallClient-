using Android.App;
using Android.Graphics;
using AnonWallClient.Services;
using System.IO;
using CommunityToolkit.Maui.Alerts;
using CommunityToolkit.Maui.Core;
using System.Text.Json;

namespace AnonWallClient.Platforms.Android;

public class WallpaperService : IWallpaperService
{
    private readonly HttpClient _httpClient;
    private readonly AppLogService _logger;
    private readonly WallpaperHistoryService _historyService;
    private readonly SettingsService _settingsService;

    public WallpaperService(IHttpClientFactory httpClientFactory, AppLogService logger, WallpaperHistoryService historyService, SettingsService settingsService)
    {
        _httpClient = httpClientFactory.CreateClient("WalltakerClient");
        _logger = logger;
        _historyService = historyService;
        _settingsService = settingsService;
    }

    public async Task<bool> SetWallpaperAsync(string imagePathOrUrl)
    {
        _logger.Add("Android Service: SetWallpaperAsync called.");
        try
        {
            var wallpaperManager = WallpaperManager.GetInstance(global::Android.App.Application.Context);
            if (wallpaperManager == null)
            {
                _logger.Add("Android Service ERROR: WallpaperManager instance is null.");
                return false;
            }
            _logger.Add("Android Service: Got WallpaperManager instance.");

            Stream imageStream;
            // Check if the input is a web URL or a local file path
            if (Uri.IsWellFormedUriString(imagePathOrUrl, UriKind.Absolute))
            {
                _logger.Add("Android Service: Path is a URL, downloading...");
                var response = await _httpClient.GetAsync(imagePathOrUrl);
                if (!response.IsSuccessStatusCode)
                {
                    _logger.Add($"Failed to download image. Status: {response.StatusCode}");
                    return false;
                }
                imageStream = await response.Content.ReadAsStreamAsync();
            }
            else
            {
                _logger.Add("Android Service: Path is a local file, opening stream...");
                // It's a local file path, open a stream to it
                imageStream = new FileStream(imagePathOrUrl, FileMode.Open, FileAccess.Read);
            }

            _logger.Add("Android Service: Decoding stream to bitmap...");
            using var bitmap = await BitmapFactory.DecodeStreamAsync(imageStream);
            await imageStream.DisposeAsync(); // Clean up the stream

            if (bitmap != null)
            {
                _logger.Add($"Android Service: Bitmap decoded successfully. Size: {bitmap.Width}x{bitmap.Height}");
                _logger.Add("Android Service: Calling native SetBitmap...");

                wallpaperManager.SetBitmap(bitmap);

                _logger.Add("Android Service: Native SetBitmap call completed.");
                await MainThread.InvokeOnMainThreadAsync(() => Toast.Make("New wallpaper set!", ToastDuration.Short).Show());
                await AddToHistory(imagePathOrUrl);
                return true;
            }
            else
            {
                _logger.Add("Android Service ERROR: Failed to decode image into bitmap.");
                return false;
            }
        }
        catch (Exception ex)
        {
            _logger.Add($"Android Service FATAL ERROR: {ex.GetType().Name} - {ex.Message}");
            return false;
        }
    }

    private async Task AddToHistory(string imageUrl)
    {
        // Fetch extra info from Walltaker API
        var linkId = _settingsService.GetLinkId();
        if (string.IsNullOrWhiteSpace(linkId)) return;
        try
        {
            var apiUrl = $"https://walltaker.joi.how/api/links/{linkId}.json";
            var response = await _httpClient.GetAsync(apiUrl);
            if (response.IsSuccessStatusCode)
            {
                var json = await response.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;
                
                // Parse fields according to actual API response structure
                var id = root.TryGetProperty("id", out var idProp) ? idProp.GetInt32() : 0;
                var postUrl = root.TryGetProperty("post_url", out var postUrlProp) ? postUrlProp.GetString() : null;
                var thumbnailUrl = root.TryGetProperty("post_thumbnail_url", out var thumbProp) ? thumbProp.GetString() : postUrl;
                var description = root.TryGetProperty("post_description", out var descProp) ? descProp.GetString() : null;
                var setBy = root.TryGetProperty("set_by", out var setByProp) ? setByProp.GetString() : null;
                var createdAt = root.TryGetProperty("created_at", out var createdProp) ? createdProp.GetDateTime() : DateTime.Now;
                var url = root.TryGetProperty("url", out var urlProp) ? urlProp.GetString() : null;
                var blacklist = root.TryGetProperty("blacklist", out var blacklistProp) ? blacklistProp.GetString() : null;
                var responseType = root.TryGetProperty("response_type", out var respTypeProp) ? respTypeProp.GetString() : null;
                var responseText = root.TryGetProperty("response_text", out var respTextProp) ? respTextProp.GetString() : null;
                
                // Create description as specified: "Set By: {{set_by}} - Post Description: {{post_description}}"
                var desc = $"Set By: {setBy ?? "Unknown"} - Post Description: {description ?? "No description available"}";
                
                // Create notes as specified: "{{id}} - {{response_type}} - {{response_text}}"
                var notes = $"{id}";
                if (!string.IsNullOrEmpty(responseType))
                    notes += $" - {responseType}";
                if (!string.IsNullOrEmpty(responseText))
                    notes += $" - {responseText}";
                
                _historyService.AddWallpaper(new WallpaperHistoryItem
                {
                    ImageUrl = postUrl ?? imageUrl,
                    ThumbnailUrl = thumbnailUrl ?? postUrl ?? imageUrl,
                    Description = desc,
                    SetTime = createdAt,
                    PostUrl = postUrl,
                    SourceUrl = url,
                    Uploader = setBy,
                    Score = null, // Not provided by Walltaker API
                    Nsfw = true, // As specified
                    Blacklisted = !string.IsNullOrEmpty(blacklist), // Convert blacklist string to boolean
                    Favorite = null, // Not provided by Walltaker API
                    Notes = notes
                });
            }
        }
        catch (Exception ex)
        {
            _logger.Add($"Failed to fetch wallpaper info: {ex.Message}");
        }
    }
}