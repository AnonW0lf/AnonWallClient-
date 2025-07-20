using AnonWallClient.Services;

namespace AnonWallClient.Services;

public class PanicService
{
    private readonly IWallpaperService _wallpaperService;
    private readonly SettingsService _settingsService;
    private readonly AppLogService _logger;

    public PanicService(IWallpaperService wallpaperService, SettingsService settingsService, AppLogService logger)
    {
        _wallpaperService = wallpaperService;
        _settingsService = settingsService;
        _logger = logger;
    }

    public async Task<bool> ExecutePanicAsync()
    {
        try
        {
            _logger.Add("Panic service: Starting panic mode execution...");
            
            // Get panic wallpaper path
            var panicPath = _settingsService.GetPanicFilePath();
            if (string.IsNullOrEmpty(panicPath))
            {
                panicPath = _settingsService.GetPanicUrl();
            }

            if (string.IsNullOrEmpty(panicPath))
            {
                _logger.Add("Panic service: No panic wallpaper configured.");
                return false;
            }

            _logger.Add($"Panic service: Using panic wallpaper: {panicPath}");

            // Set panic wallpaper for both wallpaper and lockscreen
            var wallpaperSuccess = false;
            var lockscreenSuccess = false;

            try
            {
                // Set wallpaper
                _logger.Add("Panic service: Setting wallpaper...");
                wallpaperSuccess = await _wallpaperService.SetWallpaperAsync(
                    panicPath, 
                    _settingsService.GetWallpaperFitMode(), 
                    WallpaperType.Wallpaper);
                
                if (wallpaperSuccess)
                {
                    _logger.Add("Panic service: Wallpaper set successfully.");
                }
                else
                {
                    _logger.Add("Panic service: Failed to set wallpaper.");
                }
            }
            catch (Exception ex)
            {
                _logger.Add($"Panic service: Error setting wallpaper: {ex.Message}");
            }

            try
            {
                // Set lockscreen
                _logger.Add("Panic service: Setting lockscreen...");
                lockscreenSuccess = await _wallpaperService.SetWallpaperAsync(
                    panicPath, 
                    _settingsService.GetWallpaperFitMode(), 
                    WallpaperType.Lockscreen);
                
                if (lockscreenSuccess)
                {
                    _logger.Add("Panic service: Lockscreen set successfully.");
                }
                else
                {
                    _logger.Add("Panic service: Failed to set lockscreen.");
                }
            }
            catch (Exception ex)
            {
                _logger.Add($"Panic service: Error setting lockscreen: {ex.Message}");
            }

            var overallSuccess = wallpaperSuccess || lockscreenSuccess;
            
            if (overallSuccess)
            {
                _logger.Add("Panic service: Panic mode execution completed successfully.");
            }
            else
            {
                _logger.Add("Panic service: Panic mode execution failed.");
            }

            return overallSuccess;
        }
        catch (Exception ex)
        {
            _logger.Add($"Panic service: Fatal error during panic execution: {ex.Message}");
            return false;
        }
    }

    public async Task<bool> RestoreOriginalWallpapersAsync()
    {
        try
        {
            _logger.Add("Panic service: Restoring original wallpapers...");

#if WINDOWS
            // On Windows, try to restore original lockscreen if we have registry service
            if (_wallpaperService is AnonWallClient.Platforms.Windows.WallpaperService windowsService)
            {
                try
                {
                    var lockscreenRestored = await windowsService.RestoreOriginalLockscreenAsync();
                    if (lockscreenRestored)
                    {
                        _logger.Add("Panic service: Original lockscreen restored successfully.");
                    }
                    else
                    {
                        _logger.Add("Panic service: Could not restore original lockscreen (no backup found or restore failed).");
                    }
                }
                catch (Exception ex)
                {
                    _logger.Add($"Panic service: Error restoring lockscreen: {ex.Message}");
                }
            }
#endif

            // TODO: Could also restore original desktop wallpaper if we implement backup for that
            _logger.Add("Panic service: Note - Desktop wallpaper restore not implemented yet.");
            
            return true;
        }
        catch (Exception ex)
        {
            _logger.Add($"Panic service: Error during restore: {ex.Message}");
            return false;
        }
    }
}
