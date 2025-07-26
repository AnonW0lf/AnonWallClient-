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
        _logger.Add($"Android Service: SetWallpaperAsync called for {wallpaperType}.");
        try
        {
            // Add null checks for Android context
            var context = global::Android.App.Application.Context;
            if (context == null)
            {
                _logger.Add("Android Service ERROR: Application context is null.");
                return false;
            }

            var wallpaperManager = WallpaperManager.GetInstance(context);
            if (wallpaperManager == null)
            {
                _logger.Add("Android Service ERROR: WallpaperManager instance is null.");
                return false;
            }
            _logger.Add("Android Service: Got WallpaperManager instance.");

            Stream imageStream;
            string? localPath = null;

            // Check if the input is a web URL or a local file path
            if (Uri.IsWellFormedUriString(imagePathOrUrl, UriKind.Absolute))
            {
                _logger.Add("Android Service: Path is a URL, checking cache...");
                
                // Try to get from cache first
                try
                {
                    localPath = await _cacheService.GetCachedImagePathAsync(imagePathOrUrl);
                }
                catch (Exception cacheEx)
                {
                    _logger.Add($"Android Service: Cache error: {cacheEx.Message}");
                }
                
                if (localPath != null)
                {
                    _logger.Add("Android Service: Using cached image");
                    imageStream = new FileStream(localPath, FileMode.Open, FileAccess.Read);
                }
                else
                {
                    _logger.Add("Android Service: Downloading image...");
                    var response = await _httpClient.GetAsync(imagePathOrUrl);
                    if (!response.IsSuccessStatusCode)
                    {
                        _logger.Add($"Failed to download image. Status: {response.StatusCode}");
                        return false;
                    }

                    var imageData = await response.Content.ReadAsByteArrayAsync();
                    
                    // Cache the image for future use (with error handling)
                    try
                    {
                        localPath = await _cacheService.CacheImageAsync(imagePathOrUrl, imageData);
                    }
                    catch (Exception cacheEx)
                    {
                        _logger.Add($"Android Service: Failed to cache image: {cacheEx.Message}");
                    }
                    
                    imageStream = new MemoryStream(imageData);
                }
            }
            else
            {
                _logger.Add("Android Service: Path is a local file, opening stream...");
                if (!File.Exists(imagePathOrUrl))
                {
                    _logger.Add($"Android Service ERROR: Local file does not exist: {imagePathOrUrl}");
                    return false;
                }
                imageStream = new FileStream(imagePathOrUrl, FileMode.Open, FileAccess.Read);
                localPath = imagePathOrUrl;
            }

            _logger.Add("Android Service: Decoding stream to bitmap...");
            using var originalBitmap = await BitmapFactory.DecodeStreamAsync(imageStream);
            await imageStream.DisposeAsync();

            if (originalBitmap != null)
            {
                _logger.Add($"Android Service: Bitmap decoded successfully. Size: {originalBitmap.Width}x{originalBitmap.Height}");
                
                // Apply fit mode if needed (with error handling)
                Bitmap processedBitmap;
                try
                {
                    processedBitmap = ApplyFitMode(originalBitmap, fitMode);
                }
                catch (Exception fitEx)
                {
                    _logger.Add($"Android Service: Error applying fit mode, using original: {fitEx.Message}");
                    processedBitmap = originalBitmap;
                }
                
                _logger.Add($"Android Service: Calling native SetBitmap for {wallpaperType}...");
                
                // Set wallpaper based on type
                var success = await SetWallpaperByTypeAsync(wallpaperManager, processedBitmap, wallpaperType);

                // Clean up processed bitmap if different from original
                if (processedBitmap != originalBitmap)
                {
                    try
                    {
                        processedBitmap?.Dispose();
                    }
                    catch (Exception disposeEx)
                    {
                        _logger.Add($"Android Service: Error disposing bitmap: {disposeEx.Message}");
                    }
                }

                if (success)
                {
                    _logger.Add($"Android Service: Native SetBitmap call completed for {wallpaperType}.");
                    try
                    {
                        await MainThread.InvokeOnMainThreadAsync(() => Toast.Make($"New {wallpaperType.ToString().ToLower()} set!", ToastDuration.Short).Show());
                    }
                    catch (Exception toastEx)
                    {
                        _logger.Add($"Android Service: Toast error: {toastEx.Message}");
                    }
                    
                    try
                    {
                        await AddToHistory(imagePathOrUrl, wallpaperType);
                    }
                    catch (Exception historyEx)
                    {
                        _logger.Add($"Android Service: History error: {historyEx.Message}");
                    }
                    return true;
                }
                else
                {
                    _logger.Add($"Android Service ERROR: Failed to set {wallpaperType}.");
                    return false;
                }
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
            if (ex.InnerException != null)
            {
                _logger.Add($"Android Service INNER EXCEPTION: {ex.InnerException.Message}");
            }
            return false;
        }
    }

    private async Task<bool> SetWallpaperByTypeAsync(WallpaperManager wallpaperManager, Bitmap bitmap, WallpaperType wallpaperType)
    {
        try
        {
            if (wallpaperType == WallpaperType.Lockscreen)
            {
                // Set lockscreen wallpaper only (Android 7.0+)
                if (global::Android.OS.Build.VERSION.SdkInt >= global::Android.OS.BuildVersionCodes.N)
                {
                    wallpaperManager.SetBitmap(bitmap, null, true, WallpaperManagerFlags.Lock);
                    return true;
                }
                else
                {
                    _logger.Add("Android Service: Lockscreen wallpaper not supported on this Android version. Setting system wallpaper instead.");
                    wallpaperManager.SetBitmap(bitmap);
                    return true;
                }
            }
            else
            {
                // Set home screen wallpaper only (Android 7.0+) or system wallpaper (older versions)
                if (global::Android.OS.Build.VERSION.SdkInt >= global::Android.OS.BuildVersionCodes.N)
                {
                    wallpaperManager.SetBitmap(bitmap, null, true, WallpaperManagerFlags.System);
                }
                else
                {
                    wallpaperManager.SetBitmap(bitmap);
                }
                return true;
            }
        }
        catch (Exception ex)
        {
            _logger.Add($"Android Service ERROR setting {wallpaperType}: {ex.Message}");
            return false;
        }
    }

    private Bitmap ApplyFitMode(Bitmap originalBitmap, WallpaperFitMode fitMode)
    {
        try
        {
            // Get screen dimensions
            var context = global::Android.App.Application.Context;
            var windowManager = context?.GetSystemService(global::Android.Content.Context.WindowService) as global::Android.Views.IWindowManager;
            var display = windowManager?.DefaultDisplay;
            
            if (display == null)
            {
                _logger.Add("Android Service: Could not get display info, using original bitmap");
                return originalBitmap;
            }

            var screenWidth = display.Width;
            var screenHeight = display.Height;

            _logger.Add($"Android Service: Screen size: {screenWidth}x{screenHeight}, Fit mode: {fitMode}");

            return fitMode switch
            {
                WallpaperFitMode.Fill or WallpaperFitMode.Stretch => 
                    Bitmap.CreateScaledBitmap(originalBitmap, screenWidth, screenHeight, true),
                
                WallpaperFitMode.Fit => CreateFitBitmap(originalBitmap, screenWidth, screenHeight),
                
                WallpaperFitMode.Center => CreateCenterBitmap(originalBitmap, screenWidth, screenHeight),
                
                WallpaperFitMode.Tile => CreateTileBitmap(originalBitmap, screenWidth, screenHeight),
                
                _ => originalBitmap
            };
        }
        catch (Exception ex)
        {
            _logger.Add($"Android Service: Error applying fit mode: {ex.Message}");
            return originalBitmap;
        }
    }

    private Bitmap CreateFitBitmap(Bitmap originalBitmap, int screenWidth, int screenHeight)
    {
        var originalWidth = originalBitmap.Width;
        var originalHeight = originalBitmap.Height;
        
        // Calculate scale to fit within screen while maintaining aspect ratio
        var scaleX = (float)screenWidth / originalWidth;
        var scaleY = (float)screenHeight / originalHeight;
        var scale = Math.Min(scaleX, scaleY);
        
        var newWidth = (int)(originalWidth * scale);
        var newHeight = (int)(originalHeight * scale);
        
        // Create a black background
        var resultBitmap = Bitmap.CreateBitmap(screenWidth, screenHeight, Bitmap.Config.Argb8888!)!;
        var canvas = new Canvas(resultBitmap);
        canvas.DrawColor(global::Android.Graphics.Color.Black);
        
        // Scale and center the image
        var scaledBitmap = Bitmap.CreateScaledBitmap(originalBitmap, newWidth, newHeight, true);
        var x = (screenWidth - newWidth) / 2;
        var y = (screenHeight - newHeight) / 2;
        canvas.DrawBitmap(scaledBitmap, x, y, null);
        
        scaledBitmap?.Dispose();
        return resultBitmap;
    }

    private Bitmap CreateCenterBitmap(Bitmap originalBitmap, int screenWidth, int screenHeight)
    {
        var resultBitmap = Bitmap.CreateBitmap(screenWidth, screenHeight, Bitmap.Config.Argb8888!)!;
        var canvas = new Canvas(resultBitmap);
        canvas.DrawColor(global::Android.Graphics.Color.Black);
        
        // Center the image at original size
        var x = (screenWidth - originalBitmap.Width) / 2;
        var y = (screenHeight - originalBitmap.Height) / 2;
        canvas.DrawBitmap(originalBitmap, x, y, null);
        
        return resultBitmap;
    }

    private Bitmap CreateTileBitmap(Bitmap originalBitmap, int screenWidth, int screenHeight)
    {
        var resultBitmap = Bitmap.CreateBitmap(screenWidth, screenHeight, Bitmap.Config.Argb8888!)!;
        var canvas = new Canvas(resultBitmap);
        
        var tileWidth = originalBitmap.Width;
        var tileHeight = originalBitmap.Height;
        
        // Tile the image across the screen
        for (int x = 0; x < screenWidth; x += tileWidth)
        {
            for (int y = 0; y < screenHeight; y += tileHeight)
            {
                canvas.DrawBitmap(originalBitmap, x, y, null);
            }
        }
        
        return resultBitmap;
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
                
                // Create description as specified: "Set By: {{set_by}} - Post Description: {{post_description}}"
                var desc = $"Set By: {setBy ?? "Unknown"} - Post Description: {description ?? "No description available"} ({wallpaperType})";
                
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
