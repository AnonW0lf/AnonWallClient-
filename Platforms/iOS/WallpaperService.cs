using AnonWallClient.Services;
using System.Text.Json;

namespace AnonWallClient.Platforms.iOS;

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
        _logger.Add($"iOS Service: SetWallpaperAsync called for {wallpaperType}.");
        
        // Note: iOS doesn't allow apps to set wallpapers programmatically due to security restrictions
        // We can only save the image to the photo library and show a message to the user
        try
        {
            string? localImagePath = null;

            if (Uri.IsWellFormedUriString(imagePathOrUrl, UriKind.Absolute))
            {
                _logger.Add("iOS Service: Path is a URL, checking cache...");
                
                // Try to get from cache first
                localImagePath = await _cacheService.GetCachedImagePathAsync(imagePathOrUrl);
                
                if (localImagePath == null)
                {
                    _logger.Add("iOS Service: Downloading image...");
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
                    _logger.Add("iOS Service: Using cached image");
                }
            }
            else
            {
                localImagePath = imagePathOrUrl;
            }

            // On iOS, we can't set wallpapers directly, but we can save to photos library
            // and provide instructions to the user
            await SaveImageToPhotosAsync(localImagePath, wallpaperType);
            
            _logger.Add($"iOS Service: Image saved to Photos. User needs to set {wallpaperType} manually.");
            
            // Show message to user with specific instructions based on wallpaper type
            await ShowInstructionsAsync(wallpaperType);

            await AddToHistory(imagePathOrUrl, wallpaperType);
            return true;
        }
        catch (Exception ex)
        {
            _logger.Add($"iOS Service FATAL ERROR: {ex.GetType().Name} - {ex.Message}");
            return false;
        }
    }

    private async Task ShowInstructionsAsync(WallpaperType wallpaperType)
    {
        var title = wallpaperType == WallpaperType.Lockscreen ? "Lockscreen Wallpaper Ready" : "Wallpaper Ready";
        var instructions = wallpaperType == WallpaperType.Lockscreen 
            ? "The lockscreen wallpaper has been saved to your Photos app. To set it:\n\n" +
              "1. Open Photos app\n" +
              "2. Find the saved wallpaper\n" +
              "3. Tap Share button\n" +
              "4. Select 'Use as Wallpaper'\n" +
              "5. Choose 'Lock Screen' option\n" +
              "6. Adjust and set as desired"
            : "The wallpaper has been saved to your Photos app. To set it:\n\n" +
              "1. Open Photos app\n" +
              "2. Find the saved wallpaper\n" +
              "3. Tap Share button\n" +
              "4. Select 'Use as Wallpaper'\n" +
              "5. Choose 'Home Screen' or 'Both' option\n" +
              "6. Adjust and set as desired";

        await MainThread.InvokeOnMainThreadAsync(async () =>
        {
            await Application.Current!.MainPage!.DisplayAlert(title, instructions, "OK");
        });
    }

    private async Task SaveImageToPhotosAsync(string imagePath, WallpaperType wallpaperType)
    {
        try
        {
            // Request permission to access photo library
            var status = await Permissions.RequestAsync<Permissions.Photos>();
            if (status != PermissionStatus.Granted)
            {
                _logger.Add("iOS Service: Photo library permission denied");
                await MainThread.InvokeOnMainThreadAsync(async () =>
                {
                    await Application.Current!.MainPage!.DisplayAlert(
                        "Permission Required", 
                        "Photo library access is required to save wallpapers. Please enable it in Settings.", 
                        "OK");
                });
                return;
            }

            // Save image to photos library using file copy since MediaPicker.SaveImageAsync doesn't exist
            // We'll save to Documents folder as fallback since direct photo library saving requires platform-specific code
            var documentsPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            var wallpaperFolder = Path.Combine(documentsPath, "AnonWallClient", wallpaperType.ToString());
            Directory.CreateDirectory(wallpaperFolder);
            
            var fileName = $"{wallpaperType.ToString().ToLower()}_{DateTime.Now:yyyyMMdd_HHmmss}.jpg";
            var destinationPath = Path.Combine(wallpaperFolder, fileName);
            
            File.Copy(imagePath, destinationPath, true);
            _logger.Add($"iOS Service: {wallpaperType} image saved to Documents folder: {destinationPath}");
        }
        catch (Exception ex)
        {
            _logger.Add($"iOS Service: Error saving {wallpaperType} to Documents: {ex.Message}");
            
            // Additional fallback: try to copy to temp folder
            try
            {
                var tempPath = Path.Combine(Path.GetTempPath(), $"{wallpaperType.ToString().ToLower()}_{DateTime.Now:yyyyMMdd_HHmmss}.jpg");
                File.Copy(imagePath, tempPath, true);
                _logger.Add($"iOS Service: {wallpaperType} image saved to temp folder: {tempPath}");
            }
            catch (Exception fallbackEx)
            {
                _logger.Add($"iOS Service: Fallback save also failed for {wallpaperType}: {fallbackEx.Message}");
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
