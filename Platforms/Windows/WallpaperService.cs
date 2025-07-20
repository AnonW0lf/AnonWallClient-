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
    private readonly ImageCacheService _cacheService;
    private readonly WindowsRegistryLockscreenService _lockscreenService;

    public WallpaperService(IHttpClientFactory httpClientFactory, AppLogService logger, WallpaperHistoryService historyService, SettingsService settingsService, ImageCacheService cacheService)
    {
        _httpClient = httpClientFactory.CreateClient("WalltakerClient");
        _logger = logger;
        _historyService = historyService;
        _settingsService = settingsService;
        _cacheService = cacheService;
        _lockscreenService = new WindowsRegistryLockscreenService(logger, settingsService);
    }

    [LibraryImport("user32.dll", EntryPoint = "SystemParametersInfoW", StringMarshalling = StringMarshalling.Utf16)]
    private static partial int SystemParametersInfo(int uAction, int uParam, string lpvParam, int fuWinIni);

    private const int SPI_SETDESKWALLPAPER = 20;
    private const int SPIF_UPDATEINIFILE = 0x01;
    private const int SPIF_SENDWININICHANGE = 0x02;

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
        _logger.Add($"Windows Service: Attempting to set {wallpaperType}...");
        
        try
        {
            string localImagePath;
            
            if (Uri.IsWellFormedUriString(imagePathOrUrl, UriKind.Absolute))
            {
                _logger.Add("Windows Service: Path is a URL, checking cache...");
                
                // Try to get from cache first
                localImagePath = await _cacheService.GetCachedImagePathAsync(imagePathOrUrl);
                
                if (localImagePath == null)
                {
                    _logger.Add("Windows Service: Downloading image...");
                    using var response = await _httpClient.GetAsync(imagePathOrUrl);
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
                        localImagePath = Path.Combine(Path.GetTempPath(), "wallpaper_temp.jpg");
                        await File.WriteAllBytesAsync(localImagePath, imageData);
                    }
                }
                else
                {
                    _logger.Add("Windows Service: Using cached image");
                }
            }
            else
            {
                localImagePath = imagePathOrUrl;
            }

            // Apply fit mode if needed
            var processedImagePath = ApplyFitMode(localImagePath, fitMode);

            bool success = false;

            if (wallpaperType == WallpaperType.Lockscreen)
            {
                // Use registry method for lockscreen
                _logger.Add("Windows Service: Setting lockscreen wallpaper via registry...");
                
                var registryAvailable = await _lockscreenService.IsRegistryMethodAvailableAsync();
                if (registryAvailable)
                {
                    success = await _lockscreenService.SetLockscreenWallpaperAsync(processedImagePath);
                    
                    if (success)
                    {
                        _logger.Add("Windows Service: Lockscreen wallpaper set successfully via registry.");
                    }
                    else
                    {
                        _logger.Add("Windows Service: Registry method failed, falling back to desktop wallpaper.");
                        success = SetDesktopWallpaper(processedImagePath);
                    }
                }
                else
                {
                    _logger.Add("Windows Service: Registry method not available, setting desktop wallpaper instead.");
                    success = SetDesktopWallpaper(processedImagePath);
                }
            }
            else
            {
                // Use traditional method for desktop wallpaper
                _logger.Add("Windows Service: Setting desktop wallpaper...");
                success = SetDesktopWallpaper(processedImagePath);
            }

            // Clean up temporary processed file if it's different from the original
            if (processedImagePath != localImagePath && File.Exists(processedImagePath))
            {
                try
                {
                    // Delay deletion to ensure Windows has loaded the wallpaper
                    _ = Task.Delay(2000).ContinueWith(_ => 
                    {
                        try { File.Delete(processedImagePath); } catch { }
                    });
                }
                catch { }
            }

            if (success)
            {
                var resultMessage = wallpaperType == WallpaperType.Lockscreen 
                    ? "Lockscreen wallpaper set successfully" 
                    : "Desktop wallpaper set successfully";
                    
                _logger.Add(resultMessage);
                
                // Try to add to history, but don't fail if it doesn't work
                try
                {
                    await AddToHistory(imagePathOrUrl, wallpaperType);
                }
                catch (Exception historyEx)
                {
                    _logger.Add($"Failed to add to history (but wallpaper was set): {historyEx.Message}");
                }
                
                return true;
            }
            else
            {
                _logger.Add($"Failed to set {wallpaperType}.");
                return false;
            }
        }
        catch (Exception ex)
        {
            _logger.Add($"Windows Service FATAL ERROR: {ex.GetType().Name} - {ex.Message}");
            return false;
        }
    }

    private bool SetDesktopWallpaper(string imagePath)
    {
        try
        {
            int result = SystemParametersInfo(SPI_SETDESKWALLPAPER, 0, imagePath, SPIF_UPDATEINIFILE | SPIF_SENDWININICHANGE);
            
            if (result != 0)
            {
                // Explorer restart disabled to prevent app spawning multiple instances
                _logger.Add("Windows Service: Desktop wallpaper set successfully (explorer restart disabled to prevent issues).");
            }
            
            return result != 0;
        }
        catch (Exception ex)
        {
            _logger.Add($"Windows Service: Error setting desktop wallpaper: {ex.Message}");
            return false;
        }
    }

    public async Task<bool> RestoreOriginalLockscreenAsync()
    {
        try
        {
            _logger.Add("Windows Service: Restoring original lockscreen...");
            
            if (_lockscreenService.HasBackupStored())
            {
                var success = await _lockscreenService.RestoreOriginalLockscreenAsync();
                if (success)
                {
                    _logger.Add("Windows Service: Original lockscreen restored successfully.");
                    return true;
                }
                else
                {
                    _logger.Add("Windows Service: Failed to restore original lockscreen.");
                    return false;
                }
            }
            else
            {
                _logger.Add("Windows Service: No lockscreen backup found to restore.");
                return false;
            }
        }
        catch (Exception ex)
        {
            _logger.Add($"Windows Service: Error restoring lockscreen: {ex.Message}");
            return false;
        }
    }

    private string ApplyFitMode(string imagePath, WallpaperFitMode fitMode)
    {
        try
        {
            if (fitMode == WallpaperFitMode.Fill || fitMode == WallpaperFitMode.Stretch)
            {
                // Windows handles stretching natively, so just return the original path
                return imagePath;
            }

            // Get screen dimensions using Win32 API since System.Windows.Forms.Screen isn't available
            var screenWidth = GetSystemMetrics(0); // SM_CXSCREEN
            var screenHeight = GetSystemMetrics(1); // SM_CYSCREEN

            _logger.Add($"Windows Service: Screen size: {screenWidth}x{screenHeight}, Fit mode: {fitMode}");

            using var originalImage = System.Drawing.Image.FromFile(imagePath);
            var processedImage = fitMode switch
            {
                WallpaperFitMode.Fit => CreateFitImage(originalImage, screenWidth, screenHeight),
                WallpaperFitMode.Center => CreateCenterImage(originalImage, screenWidth, screenHeight),
                WallpaperFitMode.Tile => CreateTileImage(originalImage, screenWidth, screenHeight),
                _ => null
            };

            if (processedImage != null)
            {
                var tempPath = Path.Combine(Path.GetTempPath(), $"wallpaper_processed_{Guid.NewGuid()}.jpg");
                processedImage.Save(tempPath, System.Drawing.Imaging.ImageFormat.Jpeg);
                processedImage.Dispose();
                return tempPath;
            }

            return imagePath;
        }
        catch (Exception ex)
        {
            _logger.Add($"Windows Service: Error applying fit mode: {ex.Message}");
            return imagePath;
        }
    }

    [LibraryImport("user32.dll")]
    private static partial int GetSystemMetrics(int nIndex);

    private System.Drawing.Image CreateFitImage(System.Drawing.Image originalImage, int screenWidth, int screenHeight)
    {
        // Calculate scale to fit within screen while maintaining aspect ratio
        var scaleX = (float)screenWidth / originalImage.Width;
        var scaleY = (float)screenHeight / originalImage.Height;
        var scale = Math.Min(scaleX, scaleY);
        
        var newWidth = (int)(originalImage.Width * scale);
        var newHeight = (int)(originalImage.Height * scale);
        
        // Create result image with black background
        var resultImage = new System.Drawing.Bitmap(screenWidth, screenHeight);
        using var graphics = System.Drawing.Graphics.FromImage(resultImage);
        graphics.Clear(System.Drawing.Color.Black);
        graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
        
        // Center the scaled image
        var x = (screenWidth - newWidth) / 2;
        var y = (screenHeight - newHeight) / 2;
        graphics.DrawImage(originalImage, x, y, newWidth, newHeight);
        
        return resultImage;
    }

    private System.Drawing.Image CreateCenterImage(System.Drawing.Image originalImage, int screenWidth, int screenHeight)
    {
        var resultImage = new System.Drawing.Bitmap(screenWidth, screenHeight);
        using var graphics = System.Drawing.Graphics.FromImage(resultImage);
        graphics.Clear(System.Drawing.Color.Black);
        
        // Center the image at original size
        var x = (screenWidth - originalImage.Width) / 2;
        var y = (screenHeight - originalImage.Height) / 2;
        graphics.DrawImage(originalImage, x, y, originalImage.Width, originalImage.Height);
        
        return resultImage;
    }

    private System.Drawing.Image CreateTileImage(System.Drawing.Image originalImage, int screenWidth, int screenHeight)
    {
        var resultImage = new System.Drawing.Bitmap(screenWidth, screenHeight);
        using var graphics = System.Drawing.Graphics.FromImage(resultImage);
        
        // Tile the image across the screen
        for (int x = 0; x < screenWidth; x += originalImage.Width)
        {
            for (int y = 0; y < screenHeight; y += originalImage.Height)
            {
                graphics.DrawImage(originalImage, x, y, originalImage.Width, originalImage.Height);
            }
        }
        
        return resultImage;
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
