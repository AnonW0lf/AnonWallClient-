using System.Runtime.InteropServices;
using AnonWallClient.Services;
using Microsoft.Extensions.DependencyInjection;
using System.Text.Json;

namespace AnonWallClient.Platforms.Windows;

public partial class WallpaperService : IWallpaperService
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

    [LibraryImport("user32.dll", EntryPoint = "SystemParametersInfoW", StringMarshalling = StringMarshalling.Utf16)]
    private static partial int SystemParametersInfo(int uAction, int uParam, string lpvParam, int fuWinIni);

    private const int SPI_SETDESKWALLPAPER = 20;
    private const int SPIF_UPDATEINIFILE = 0x01;
    private const int SPIF_SENDWININICHANGE = 0x02;

    public async Task<bool> SetWallpaperAsync(string imagePathOrUrl)
    {
        _logger.Add("Attempting to set Windows wallpaper...");
        bool wallpaperSetSuccessfully = false;
        
        try
        {
            string tempPath;
            if (Uri.IsWellFormedUriString(imagePathOrUrl, UriKind.Absolute))
            {
                using var response = await _httpClient.GetAsync(imagePathOrUrl);
                if (!response.IsSuccessStatusCode)
                {
                    _logger.Add($"Failed to download image. Status: {response.StatusCode}");
                    return false;
                }

                tempPath = Path.Combine(Path.GetTempPath(), "wallpaper.jpg");
                await using var fs = new FileStream(tempPath, FileMode.Create, FileAccess.Write);
                await response.Content.CopyToAsync(fs);
                await fs.DisposeAsync();
            }
            else
            {
                tempPath = imagePathOrUrl;
            }

            int result = SystemParametersInfo(SPI_SETDESKWALLPAPER, 0, tempPath, SPIF_UPDATEINIFILE | SPIF_SENDWININICHANGE);
            if (result != 0)
            {
                _logger.Add("Wallpaper set successfully.");
                wallpaperSetSuccessfully = true;
                
                // Try to add to history, but don't fail if it doesn't work
                try
                {
                    await AddToHistory(imagePathOrUrl);
                }
                catch (Exception historyEx)
                {
                    _logger.Add($"Failed to add to history (but wallpaper was set): {historyEx.Message}");
                }
                
                return true;
            }
            else
            {
                _logger.Add("System call failed to set wallpaper.");
                return false;
            }
        }
        catch (Exception ex)
        {
            _logger.Add($"ERROR setting Windows wallpaper: {ex.Message}");
            return wallpaperSetSuccessfully; // Return true if wallpaper was actually set despite other errors
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