using AnonWallClient.Services;

namespace AnonWallClient.Services;

public class WallpaperManagementService
{
    private readonly IWallpaperService _wallpaperService;
    private readonly WallpaperHistoryService _historyService;
    private readonly SettingsService _settingsService;
    private readonly AppLogService _logger;

    public WallpaperManagementService(
        IWallpaperService wallpaperService,
        WallpaperHistoryService historyService,
        SettingsService settingsService,
        AppLogService logger)
    {
        _wallpaperService = wallpaperService;
        _historyService = historyService;
        _settingsService = settingsService;
        _logger = logger;
    }

    /// <summary>
    /// Reapplies current wallpapers with new fit mode settings
    /// </summary>
    public async Task<bool> ReapplyCurrentWallpapersAsync()
    {
        try
        {
            _logger.Add("WallpaperManagement: Reapplying current wallpapers with updated settings...");

            var currentWallpaper = _historyService.History?.FirstOrDefault(w => w.WallpaperType == WallpaperType.Wallpaper);
            var currentLockscreen = _historyService.History?.FirstOrDefault(w => w.WallpaperType == WallpaperType.Lockscreen);
            
            var fitMode = _settingsService.GetWallpaperFitMode();
            bool anySuccess = false;

            // Reapply wallpaper if exists
            if (currentWallpaper != null && !string.IsNullOrEmpty(currentWallpaper.ImageUrl))
            {
                _logger.Add("WallpaperManagement: Reapplying desktop wallpaper...");
                var success = await _wallpaperService.SetWallpaperAsync(
                    currentWallpaper.ImageUrl, 
                    fitMode, 
                    WallpaperType.Wallpaper);
                
                if (success)
                {
                    _logger.Add("WallpaperManagement: Desktop wallpaper reapplied successfully.");
                    anySuccess = true;
                }
                else
                {
                    _logger.Add("WallpaperManagement: Failed to reapply desktop wallpaper.");
                }
            }

            // Reapply lockscreen if exists and different from wallpaper
            if (currentLockscreen != null && 
                !string.IsNullOrEmpty(currentLockscreen.ImageUrl) && 
                currentLockscreen.ImageUrl != currentWallpaper?.ImageUrl)
            {
                _logger.Add("WallpaperManagement: Reapplying lockscreen wallpaper...");
                var success = await _wallpaperService.SetWallpaperAsync(
                    currentLockscreen.ImageUrl, 
                    fitMode, 
                    WallpaperType.Lockscreen);
                
                if (success)
                {
                    _logger.Add("WallpaperManagement: Lockscreen wallpaper reapplied successfully.");
                    anySuccess = true;
                }
                else
                {
                    _logger.Add("WallpaperManagement: Failed to reapply lockscreen wallpaper.");
                }
            }

            if (!anySuccess && (currentWallpaper != null || currentLockscreen != null))
            {
                _logger.Add("WallpaperManagement: Warning - No wallpapers were successfully reapplied.");
            }
            else if (currentWallpaper == null && currentLockscreen == null)
            {
                _logger.Add("WallpaperManagement: No current wallpapers to reapply.");
            }

            return anySuccess;
        }
        catch (Exception ex)
        {
            _logger.Add($"WallpaperManagement: Error reapplying wallpapers: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Rolls back to the previous wallpaper (used for disgust responses)
    /// </summary>
    public async Task<bool> RollbackToPreviousWallpaperAsync(WallpaperType wallpaperType = WallpaperType.Wallpaper)
    {
        try
        {
            _logger.Add($"WallpaperManagement: Rolling back {wallpaperType} to previous wallpaper...");

            var history = _historyService.History?.Where(w => w.WallpaperType == wallpaperType).ToList();
            if (history == null || history.Count < 2)
            {
                _logger.Add($"WallpaperManagement: Not enough {wallpaperType} history to rollback (need at least 2 entries).");
                return false;
            }

            // Get the previous wallpaper (second in history)
            var previousWallpaper = history[1];
            var fitMode = _settingsService.GetWallpaperFitMode();

            _logger.Add($"WallpaperManagement: Rolling back to: {previousWallpaper.Description}");

            var success = await _wallpaperService.SetWallpaperAsync(
                previousWallpaper.ImageUrl, 
                fitMode, 
                wallpaperType);

            if (success)
            {
                _logger.Add($"WallpaperManagement: Successfully rolled back {wallpaperType}.");
                
                // Remove the current (disliked) wallpaper from history
                var currentWallpaper = history[0];
                _historyService.RemoveWallpaper(currentWallpaper);
                
                _logger.Add($"WallpaperManagement: Removed disliked wallpaper from history.");
                return true;
            }
            else
            {
                _logger.Add($"WallpaperManagement: Failed to rollback {wallpaperType}.");
                return false;
            }
        }
        catch (Exception ex)
        {
            _logger.Add($"WallpaperManagement: Error during rollback: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Gets the current wallpaper info for display purposes
    /// </summary>
    public (WallpaperHistoryItem? wallpaper, WallpaperHistoryItem? lockscreen) GetCurrentWallpapers()
    {
        try
        {
            var wallpaper = _historyService.History?.FirstOrDefault(w => w.WallpaperType == WallpaperType.Wallpaper);
            var lockscreen = _historyService.History?.FirstOrDefault(w => w.WallpaperType == WallpaperType.Lockscreen);
            
            return (wallpaper, lockscreen);
        }
        catch (Exception ex)
        {
            _logger.Add($"WallpaperManagement: Error getting current wallpapers: {ex.Message}");
            return (null, null);
        }
    }
}
