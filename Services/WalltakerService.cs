using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using AnonWallClient.Models;

namespace AnonWallClient.Services;

public class WalltakerService(IHttpClientFactory httpClientFactory, WallpaperHistoryService historyService)
{
    private string? _lastWallpaperImageUrl = null;
    private string? _lastLockscreenImageUrl = null;
    private readonly WallpaperHistoryService _historyService = historyService;

    public async Task<string?> GetNewWallpaperUrlAsync(string linkId, WallpaperType wallpaperType = WallpaperType.Wallpaper)
    {
        if (string.IsNullOrWhiteSpace(linkId))
        {
            return null;
        }

        var httpClient = httpClientFactory.CreateClient("WalltakerClient");
        var url = $"https://walltaker.joi.how/api/links/{linkId}.json";

        try
        {
            var response = await httpClient.GetFromJsonAsync<LinkData>(url);
            var newImageUrl = response?.PostUrl;

            if (!string.IsNullOrEmpty(newImageUrl))
            {
                // Get the appropriate last image URL based on wallpaper type
                var lastImageUrl = wallpaperType == WallpaperType.Lockscreen 
                    ? _lastLockscreenImageUrl 
                    : _lastWallpaperImageUrl;

                // Initialize last image URL from history if not set (app startup)
                if (lastImageUrl == null)
                {
                    var historyForType = wallpaperType == WallpaperType.Lockscreen 
                        ? _historyService.GetLockscreenHistory() 
                        : _historyService.GetWallpaperHistory();
                        
                    if (historyForType.Any())
                    {
                        lastImageUrl = historyForType.First().ImageUrl;
                        
                        // Update the appropriate last image URL
                        if (wallpaperType == WallpaperType.Lockscreen)
                            _lastLockscreenImageUrl = lastImageUrl;
                        else
                            _lastWallpaperImageUrl = lastImageUrl;
                    }
                }

                // Check if this is truly a new wallpaper
                if (newImageUrl != lastImageUrl)
                {
                    // Update the appropriate last image URL
                    if (wallpaperType == WallpaperType.Lockscreen)
                        _lastLockscreenImageUrl = newImageUrl;
                    else
                        _lastWallpaperImageUrl = newImageUrl;
                        
                    return newImageUrl;
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"API Error for {wallpaperType}: {ex.Message}");
        }

        return null;
    }

    // Legacy method for backward compatibility
    public async Task<string?> GetNewWallpaperUrlAsync(string linkId)
    {
        return await GetNewWallpaperUrlAsync(linkId, WallpaperType.Wallpaper);
    }

    public async Task<(bool Success, string ErrorMessage)> PostResponseAsync(string linkId, string apiKey, string responseType, string? responseText = null)
    {
        if (string.IsNullOrWhiteSpace(linkId) || string.IsNullOrWhiteSpace(apiKey))
        {
            return (false, "Link ID or API Key is missing.");
        }

        var httpClient = httpClientFactory.CreateClient("WalltakerClient");
        var url = $"https://walltaker.joi.how/api/links/{linkId}/response.json";

        var payload = new ResponseData
        {
            ApiKey = apiKey,
            ResponseType = responseType,
            Text = responseText
        };

        var jsonContent = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

        try
        {
            var response = await httpClient.PostAsync(url, jsonContent);
            if (response.IsSuccessStatusCode)
            {
                return (true, string.Empty);
            }
            else
            {
                var errorBody = await response.Content.ReadAsStringAsync();
                return (false, $"Server returned error: {response.StatusCode}. Details: {errorBody}");
            }
        }
        catch (Exception ex)
        {
            return (false, $"Exception: {ex.Message}");
        }
    }

    // New method to check multiple LinkIDs and return results with their types
    public async Task<List<(string ImageUrl, WallpaperType Type)>> CheckMultipleLinkIdsAsync(string wallpaperLinkId, string lockscreenLinkId, LinkIdMode linkIdMode)
    {
        var results = new List<(string ImageUrl, WallpaperType Type)>();

        if (linkIdMode == LinkIdMode.SharedLink)
        {
            // Use the same LinkId for both wallpaper and lockscreen
            var sharedLinkId = !string.IsNullOrEmpty(wallpaperLinkId) ? wallpaperLinkId : lockscreenLinkId;
            
            if (!string.IsNullOrEmpty(sharedLinkId))
            {
                var wallpaperUrl = await GetNewWallpaperUrlAsync(sharedLinkId, WallpaperType.Wallpaper);
                if (!string.IsNullOrEmpty(wallpaperUrl))
                {
                    results.Add((wallpaperUrl, WallpaperType.Wallpaper));
                    // For shared mode, also add as lockscreen
                    results.Add((wallpaperUrl, WallpaperType.Lockscreen));
                }
            }
        }
        else
        {
            // Use separate LinkIds for wallpaper and lockscreen
            if (!string.IsNullOrEmpty(wallpaperLinkId))
            {
                var wallpaperUrl = await GetNewWallpaperUrlAsync(wallpaperLinkId, WallpaperType.Wallpaper);
                if (!string.IsNullOrEmpty(wallpaperUrl))
                {
                    results.Add((wallpaperUrl, WallpaperType.Wallpaper));
                }
            }

            if (!string.IsNullOrEmpty(lockscreenLinkId))
            {
                var lockscreenUrl = await GetNewWallpaperUrlAsync(lockscreenLinkId, WallpaperType.Lockscreen);
                if (!string.IsNullOrEmpty(lockscreenUrl))
                {
                    results.Add((lockscreenUrl, WallpaperType.Lockscreen));
                }
            }
        }

        return results;
    }
}
