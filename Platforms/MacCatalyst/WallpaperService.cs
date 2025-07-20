using AnonWallClient.Services;
using System.Text.Json;

namespace AnonWallClient.Platforms.MacCatalyst;

public class WallpaperService : IWallpaperService
{
    private readonly HttpClient _httpClient;
    private readonly AppLogService _logger;
    private readonly WallpaperHistoryService _historyService;
    private readonly SettingsService _settingsService;
    private readonly ImageCacheService _cacheService;

    public WallpaperService(IHttpClientFactory httpClientFactory, AppLogService logger, WallpaperHistoryService historyService, SettingsService settingsService, ImageCacheService cacheService)
    {
        _httpClient = httpClientFactory.CreateClient("WalltakerClient");
        _logger = logger;
        _historyService = historyService;
        _settingsService = settingsService;
        _cacheService = cacheService;
    }

    public async Task<bool> SetWallpaperAsync(string imagePathOrUrl)
    {
        return await SetWallpaperAsync(imagePathOrUrl, _settingsService.GetWallpaperFitMode());
    }

    public async Task<bool> SetWallpaperAsync(string imagePathOrUrl, WallpaperFitMode fitMode)
    {
        return await SetWallpaperAsync(imagePathOrUrl, fitMode, WallpaperType.Wallpaper);
    }

    public async Task<bool> SetWallpaperAsync(string imagePathOrUrl, WallpaperFitMode fitMode, WallpaperType wallpaperType)
    {
        _logger.Add($"macOS Service: SetWallpaperAsync called for {wallpaperType}.");
        
        try
        {
            string? localImagePath = null;

            if (Uri.IsWellFormedUriString(imagePathOrUrl, UriKind.Absolute))
            {
                _logger.Add("macOS Service: Path is a URL, checking cache...");
                
                // Try to get from cache first
                localImagePath = await _cacheService.GetCachedImagePathAsync(imagePathOrUrl);
                
                if (localImagePath == null)
                {
                    _logger.Add("macOS Service: Downloading image...");
                    var response = await _httpClient.GetAsync(imagePathOrUrl);
                    if (!response.IsSuccessStatusCode)
                    {
                        _logger.Add($"Failed to download image. Status: {response.StatusCode}");
                        return false;
                    }

                    var imageData = await response.Content.ReadAsByteArrayAsync();
                    
                    // Cache the image for future use
                    localImagePath = await _cacheService.CacheImageAsync(imagePathOrUrl, imageData);
                    
                    // If caching failed, save to temp file
                    if (localImagePath == null)
                    {
                        var tempPath = Path.Combine(Path.GetTempPath(), $"wallpaper_{Guid.NewGuid()}.jpg");
                        await File.WriteAllBytesAsync(tempPath, imageData);
                        localImagePath = tempPath;
                    }
                }
                else
                {
                    _logger.Add("macOS Service: Using cached image");
                }
            }
            else
            {
                localImagePath = imagePathOrUrl;
            }

            // Use AppleScript to set wallpaper on macOS
            var success = await SetWallpaperUsingAppleScriptAsync(localImagePath, fitMode, wallpaperType);
            
            if (success)
            {
                _logger.Add($"macOS Service: {wallpaperType} set successfully");
                await AddToHistory(imagePathOrUrl, wallpaperType);
                
                // Show success notification
                await MainThread.InvokeOnMainThreadAsync(async () =>
                {
                    await Application.Current!.MainPage!.DisplayAlert("Success", $"{wallpaperType} set successfully!", "OK");
                });
                
                return true;
            }
            else
            {
                _logger.Add($"macOS Service: Failed to set {wallpaperType}");
                return false;
            }
        }
        catch (Exception ex)
        {
            _logger.Add($"macOS Service FATAL ERROR: {ex.GetType().Name} - {ex.Message}");
            return false;
        }
    }

    private async Task<bool> SetWallpaperUsingAppleScriptAsync(string imagePath, WallpaperFitMode fitMode, WallpaperType wallpaperType)
    {
        try
        {
            // Convert fit mode to macOS equivalent
            var macOSFitMode = fitMode switch
            {
                WallpaperFitMode.Fill => "fill screen",
                WallpaperFitMode.Fit => "fit to screen",
                WallpaperFitMode.Center => "center",
                WallpaperFitMode.Tile => "tile",
                WallpaperFitMode.Stretch => "stretch to fill screen",
                _ => "fill screen"
            };

            // Create AppleScript to set wallpaper
            // Note: macOS typically doesn't have separate lockscreen wallpapers like iOS
            // The same wallpaper is usually used for both desktop and lock screen
            var script = $@"
tell application ""System Events""
    tell every desktop
        set picture to ""{imagePath}""
        set picture rotation to 0
        set picture size to {macOSFitMode}
    end tell
end tell
";

            // Execute AppleScript using osascript command
            var processInfo = new System.Diagnostics.ProcessStartInfo
            {
                FileName = "/usr/bin/osascript",
                Arguments = "-e " + script.Replace("\"", "\\\""),
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            using var process = System.Diagnostics.Process.Start(processInfo);
            if (process != null)
            {
                await process.WaitForExitAsync();
                
                var output = await process.StandardOutput.ReadToEndAsync();
                var error = await process.StandardError.ReadToEndAsync();
                
                if (process.ExitCode == 0)
                {
                    _logger.Add($"macOS Service: AppleScript executed successfully for {wallpaperType}");
                    return true;
                }
                else
                {
                    _logger.Add($"macOS Service: AppleScript failed for {wallpaperType}. Error: {error}");
                    return false;
                }
            }
            
            return false;
        }
        catch (Exception ex)
        {
            _logger.Add($"macOS Service: Error executing AppleScript for {wallpaperType}: {ex.Message}");
            
            // Fallback: try to save to Documents and show instructions
            try
            {
                var documentsPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                var wallpaperFolder = Path.Combine(documentsPath, "AnonWallClient", wallpaperType.ToString());
                Directory.CreateDirectory(wallpaperFolder);
                
                var fileName = $"{wallpaperType.ToString().ToLower()}_{DateTime.Now:yyyyMMdd_HHmmss}.jpg";
                var destinationPath = Path.Combine(wallpaperFolder, fileName);
                
                File.Copy(imagePath, destinationPath, true);
                _logger.Add($"macOS Service: {wallpaperType} saved to Documents: {destinationPath}");
                
                var instructions = wallpaperType == WallpaperType.Lockscreen 
                    ? "Note: macOS uses the same image for desktop and lock screen. The wallpaper has been saved to:\n"
                    : "The wallpaper has been saved to:\n";

                await MainThread.InvokeOnMainThreadAsync(async () =>
                {
                    await Application.Current!.MainPage!.DisplayAlert(
                        $"{wallpaperType} Saved", 
                        $"{instructions}{destinationPath}\n\n" +
                        "To set it manually:\n" +
                        "1. Open System Preferences\n" +
                        "2. Go to Desktop & Screen Saver\n" +
                        "3. Click '+' and select the saved image", 
                        "OK");
                });
                
                return true; // Consider it successful since we saved the file
            }
            catch (Exception fallbackEx)
            {
                _logger.Add($"macOS Service: Fallback save also failed for {wallpaperType}: {fallbackEx.Message}");
                return false;
            }
        }
    }

    private async Task AddToHistory(string imageUrl, WallpaperType wallpaperType)
    {
        // Determine which LinkId to use based on wallpaper type
        var linkId = wallpaperType == WallpaperType.Lockscreen 
            ? _settingsService.GetLockscreenLinkId() 
            : _settingsService.GetWallpaperLinkId();

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
                
                // Create description and notes
                var desc = $"Set By: {setBy ?? "Unknown"} - Post Description: {description ?? "No description available"} ({wallpaperType})";
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
                    Score = null,
                    Nsfw = true,
                    Blacklisted = !string.IsNullOrEmpty(blacklist),
                    Favorite = null,
                    Notes = notes,
                    WallpaperType = wallpaperType
                });
            }
        }
        catch (Exception ex)
        {
            _logger.Add($"Failed to fetch wallpaper info: {ex.Message}");
        }
    }
}
